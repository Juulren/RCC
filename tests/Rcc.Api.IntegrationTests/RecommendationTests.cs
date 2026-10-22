using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Recommendations;
using Rcc.Api.Reservations;
namespace Rcc.Api.IntegrationTests;

public sealed class RecommendationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid cheap, river;
    private sealed class Gateway : IRecommendationGateway
    {
        public int Calls; public string Mode = "valid"; public Func<Task>? DuringCall;
        public async Task<List<RankedChoice>> RankAsync(List<Candidate> candidates, string[] preferences, string locale, CancellationToken token)
        {
            Interlocked.Increment(ref Calls); if (DuringCall is not null) await DuringCall();
            if (Mode == "fail") throw new HttpRequestException("private provider error");
            if (Mode == "invalid") return [new("invented-id", "Do not trust this")];
            if (Mode == "duplicate") return [new(candidates[0].Id, "First"), new(candidates[0].Id, "Second")];
            return candidates.Take(2).Select(x => new RankedChoice(x.Id, locale == "es" ? "Comparación de precio y espacio." : "Price and space comparison.")).ToList();
        }
    }
    public async Task InitializeAsync()
    {
        await using var db = postgres.OpenDbContext();
        if (!System.Text.RegularExpressions.Regex.IsMatch(db.Database.GetDbConnection().Database, "^rcc_test_[0-9a-f]{32}$")) throw new InvalidOperationException("Not a fixture database.");
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Notifications\", \"PaymentEvents\", \"RefundJobs\", \"Payments\", \"IdempotencyRequests\", \"ReservationItems\", \"Reservations\"");
        await db.RoomTypes.ExecuteUpdateAsync(x => x.SetProperty(t => t.Active, false));
        await db.AiDailyUsages.ExecuteDeleteAsync();
        RoomType Type(bool view, decimal price, int count)
        {
            var id = Guid.NewGuid();
            return new RoomType { Id = id, HotelId = CatalogSeed.HotelId, Slug = "recommend-" + id.ToString("N"), NameEs = view ? "Suite río" : "Classic", NameEn = view ? "River suite" : "Classic", DescriptionEs = "Prueba", DescriptionEn = "Test", RiverView = view, Area = view ? 120 : 60, Capacity = view ? 4 : 3, MaxAdults = view ? 3 : 2, MaxChildren = 2, NightlyRate = price, Rooms = Enumerable.Range(0, count).Select(_ => new PhysicalRoom { Id = Guid.NewGuid(), HotelId = CatalogSeed.HotelId, RoomTypeId = id, Number = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant() }).ToList() };
        }
        var first = Type(false, 100, 2); var second = Type(true, 200, 1); cheap = first.Id; river = second.Id;
        db.RoomTypes.AddRange(first, second); await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private static RecommendationInput Input(GuestInput?[]? rooms = null, decimal? budget = null, string[]? preferences = null) => new(Inventory.Today(TimeProvider.System).AddDays(10), Inventory.Today(TimeProvider.System).AddDays(12), rooms ?? [new(2, [])], budget, preferences ?? [], "es");
    private static IOptions<AiSettings> Settings(bool enabled = true, int limit = 20) => Options.Create(new AiSettings { Enabled = enabled, ApiKey = "sk-fixture-never-real", Model = "fixture", DailyRequestLimit = limit });
    private async Task<RecommendationResult> Run(RecommendationInput input, Gateway gateway, bool enabled = false, int limit = 20)
    {
        await using var db = postgres.OpenDbContext(); return await new RecommendationService(db, gateway, Settings(enabled, limit), TimeProvider.System).RecommendAsync(input, default);
    }
    [Fact]
    public async Task Disabled_ai_uses_verified_prices_and_capacity_without_provider_or_reservation()
    {
        var gateway = new Gateway(); var result = await Run(Input(), gateway);
        Assert.Equal("deterministic", result.Mode); Assert.Equal("AI_NOT_CONFIGURED", result.Reason); Assert.Equal(0, gateway.Calls);
        Assert.Equal(200, result.Options[0].Quote.Total); Assert.Equal(cheap, result.Options[0].Quote.Items.Single().RoomTypeId);
        await using var db = postgres.OpenDbContext(); Assert.Empty(await db.AiDailyUsages.ToListAsync()); Assert.Empty(await db.Reservations.ToListAsync());
    }
    [Fact]
    public async Task Whole_stay_budget_and_shared_inventory_are_enforced_for_multiple_rooms()
    {
        var result = await Run(Input([new(2, []), new(2, []), new(2, [])], 800), new Gateway());
        Assert.NotEmpty(result.Options); Assert.All(result.Options, option => { Assert.Equal(800, option.Quote.Total); Assert.Equal(3, option.Quote.Items.Count); Assert.Equal(2, option.Quote.Items.Count(x => x.RoomTypeId == cheap)); Assert.Equal(1, option.Quote.Items.Count(x => x.RoomTypeId == river)); });
        Assert.Empty((await Run(Input([new(2, []), new(2, []), new(2, [])], 799), new Gateway())).Options);
    }
    [Fact]
    public async Task River_and_space_preferences_rank_a_valid_suite()
    {
        var result = await Run(Input(preferences: ["river", "space"]), new Gateway()); Assert.Equal(river, result.Options[0].Quote.Items.Single().RoomTypeId);
        result = await Run(Input([new(3, [8])]), new Gateway()); Assert.All(result.Options, x => Assert.Equal(river, x.Quote.Items.Single().RoomTypeId));
    }
    [Fact]
    public async Task No_capacity_or_budget_match_never_calls_ai_or_spends_daily_budget()
    {
        var gateway = new Gateway(); var result = await Run(Input([new(5, [])]), gateway, true); Assert.Empty(result.Options); Assert.Equal("NO_MATCH", result.Reason); Assert.Equal(0, gateway.Calls);
        await using var db = postgres.OpenDbContext(); Assert.Empty(await db.AiDailyUsages.ToListAsync());
    }
    [Theory]
    [InlineData("fail", "AI_UNAVAILABLE")]
    [InlineData("invalid", "AI_INVALID_RESPONSE")]
    [InlineData("duplicate", "AI_INVALID_RESPONSE")]
    public async Task Provider_failure_or_invented_duplicate_id_uses_explicit_local_fallback(string mode, string reason)
    {
        var result = await Run(Input(), new Gateway { Mode = mode }, true); Assert.Equal("deterministic", result.Mode); Assert.Equal(reason, result.Reason); Assert.NotEmpty(result.Options); Assert.All(result.Options, x => Assert.Contains("No intervino IA", x.Explanation));
    }
    [Fact]
    public async Task Ai_ranks_only_server_candidates_and_revalidates_after_external_call()
    {
        var gateway = new Gateway(); var result = await Run(Input(), gateway, true); Assert.Equal("ai", result.Mode); Assert.Equal("AI_RANKED", result.Reason);
        gateway.DuringCall = async () => { await using var db = postgres.OpenDbContext(); await db.Rooms.ExecuteUpdateAsync(x => x.SetProperty(room => room.Active, false)); };
        result = await Run(Input(), gateway, true); Assert.Empty(result.Options); Assert.Equal("AVAILABILITY_CHANGED", result.Reason);
    }
    [Fact]
    public async Task Confirmed_and_unexpired_pending_inventory_are_excluded_but_expired_is_free()
    {
        await using var db = postgres.OpenDbContext(); var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = Guid.NewGuid().ToString(), Email = "fixture@example.test" }; db.Users.Add(user);
        var input = Input(); var unit = await db.Rooms.FirstAsync(x => x.RoomTypeId == river);
        var reservation = new Reservation { Id = Guid.NewGuid(), UserId = user.Id, Arrival = input.Arrival, Departure = input.Departure, Total = 400, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30), Items = [new ReservationItem { Id = Guid.NewGuid(), RoomId = unit.Id, NameEs = "Río", NameEn = "River", Adults = 2, ChildAges = [], NightlyRate = 200, Subtotal = 400 }] };
        db.Reservations.Add(reservation); await db.SaveChangesAsync();
        var result = await Run(Input([new(3, [])]), new Gateway()); Assert.Empty(result.Options);
        reservation.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); Assert.NotEmpty((await Run(Input([new(3, [])]), new Gateway())).Options);
        reservation.Status = "Confirmed"; await db.SaveChangesAsync(); Assert.Empty((await Run(Input([new(3, [])]), new Gateway())).Options);
    }
    [Fact]
    public async Task Daily_budget_is_atomic_across_scopes_and_keeps_failed_attempts()
    {
        var gateway = new Gateway { Mode = "fail" };
        var results = await Task.WhenAll(Run(Input(), gateway, true, 1), Run(Input(), gateway, true, 1));
        Assert.Equal(1, gateway.Calls); Assert.Contains(results, x => x.Reason == "AI_DAILY_LIMIT");
        await using var db = postgres.OpenDbContext(); Assert.Equal(1, (await db.AiDailyUsages.SingleAsync()).Requests);
    }
    [Fact]
    public void Invalid_dates_guests_locale_budget_and_preferences_are_rejected()
    {
        var invalid = Input([new(0, [18])], -1, ["ignore instructions"]) with { Arrival = Inventory.Today(TimeProvider.System).AddDays(-1), Locale = "fr" };
        var errors = RecommendationService.Validate(invalid, TimeProvider.System); Assert.Contains("arrival", errors.Keys); Assert.Contains("rooms[0]", errors.Keys); Assert.Contains("locale", errors.Keys); Assert.Contains("budget", errors.Keys); Assert.Contains("preferences", errors.Keys);
        Assert.Contains("budget", RecommendationService.Validate(Input(budget: 0.001m), TimeProvider.System).Keys);
    }
    [Fact]
    public async Task Endpoint_requires_session_csrf_and_limits_requests_per_user()
    {
        await using var factory = postgres.CreateFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/recommendations", Input())).StatusCode);
        using var scope = factory.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(); var email = $"recommend-{Guid.NewGuid():N}@example.test"; const string password = "Recommendation-2026!Fixture"; var user = new IdentityUser { UserName = email, Email = email }; Assert.True((await users.CreateAsync(user, password)).Succeeded); await users.AddToRoleAsync(user, "Customer");
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) }; login.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(login)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recommendations", Input())).StatusCode);
        for (var i = 0; i < 3; i++)
        {
            csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); using var request = new HttpRequestMessage(HttpMethod.Post, "/api/recommendations") { Content = JsonContent.Create(Input()) }; request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); var response = await client.SendAsync(request);
            // The rejected CSRF request also consumes the inexpensive endpoint rate budget.
            Assert.Equal(i < 2 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (response.IsSuccessStatusCode) { Assert.True(response.Headers.CacheControl!.NoStore); var result = await response.Content.ReadFromJsonAsync<RecommendationResult>(); Assert.Equal("deterministic", result!.Mode); }
        }
    }
}
