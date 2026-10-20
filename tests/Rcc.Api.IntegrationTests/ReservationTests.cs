using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rcc.Api.Catalog;
using Rcc.Api.Data;
using Rcc.Api.Reservations;

namespace Rcc.Api.IntegrationTests;

public sealed class ReservationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 16, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private WebApplicationFactory<Program> Factory(Clock? clock = null) => postgres.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock ?? new Clock());
    }));
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> factory, bool admin = false)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"reservation-{Guid.NewGuid():N}@example.test";
        var user = new IdentityUser { UserName = email, Email = email };
        const string password = "Reservations-2026!Test";
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, admin ? "Admin" : "Customer")).Succeeded);
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, "/auth/login", new { email, password })).StatusCode);
        return client;
    }
    private static async Task<HttpResponseMessage> Write(HttpClient client, string path, object body, string? key = null, bool csrf = true, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, "/api" + path) { Content = JsonContent.Create(body) };
        if (csrf)
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private async Task<Guid> CreateInventory(int count = 1, decimal rate = 100.25m)
    {
        await using var db = postgres.OpenDbContext();
        var type = new RoomType { Id = Guid.NewGuid(), HotelId = CatalogSeed.HotelId, Slug = "reserve-" + Guid.NewGuid().ToString("N"), NameEs = "Espacio original", NameEn = "Original space", DescriptionEs = "Prueba", DescriptionEn = "Test", Category = "room", Beds = "king", Area = 60, Capacity = 4, MaxAdults = 3, MaxChildren = 2, NightlyRate = rate };
        type.Rooms = Enumerable.Range(0, count).Select(i => new PhysicalRoom { Id = Guid.NewGuid(), HotelId = type.HotelId, RoomTypeId = type.Id, Number = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() }).ToList();
        db.RoomTypes.Add(type); await db.SaveChangesAsync(); return type.Id;
    }
    private static StayInput Stay(Guid id, int count = 1) => new(new(2026, 11, 10), new(2026, 11, 12), Enumerable.Range(0, count).Select(_ => (GuestInput?)new GuestInput(2, [4], id)).ToArray());
    private static async Task<ReserveInput> Payload(HttpClient client, StayInput stay)
    {
        var response = await client.PostAsJsonAsync("/api/quotes", stay);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<QuoteView>())!;
        return new(stay.Arrival, stay.Departure, stay.Rooms, quote.PriceFingerprint, "es");
    }
    private static Task<HttpResponseMessage> Reserve(HttpClient client, ReserveInput input, string? key = null) => Write(client, "/reservations", input, key ?? Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Availability_matches_capacity_operational_status_and_shared_stock()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var id = await CreateInventory(2); var stay = Stay(id);
        var response = await client.PostAsJsonAsync("/api/availability", stay);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var option = data.GetProperty("selections")[0].GetProperty("options").EnumerateArray().Single(x => x.GetProperty("roomType").GetProperty("id").GetGuid() == id);
        Assert.Equal(2, option.GetProperty("availableCount").GetInt32());
        var tooManyAdults = stay with { Rooms = [new GuestInput(4, [], id)] };
        data = await (await client.PostAsJsonAsync("/api/availability", tooManyAdults)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(data.GetProperty("selections")[0].GetProperty("options").EnumerateArray(), x => x.GetProperty("roomType").GetProperty("id").GetGuid() == id);
        await using var db = postgres.OpenDbContext();
        await db.Rooms.Where(x => x.RoomTypeId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", stay)).StatusCode);
    }

    [Theory]
    [InlineData("past")]
    [InlineData("same")]
    [InlineData("long")]
    [InlineData("far")]
    [InlineData("adult")]
    [InlineData("age")]
    [InlineData("rooms")]
    [InlineData("null")]
    public async Task Invalid_dates_distribution_and_child_ages_are_rejected(string kind)
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var input = Stay(Guid.NewGuid());
        input = kind switch
        {
            "past" => input with { Arrival = new(2026, 11, 1) }, // UTC Nov 1, Chongqing already Nov 2.
            "same" => input with { Departure = input.Arrival },
            "long" => input with { Departure = input.Arrival.AddDays(31) },
            "far" => input with { Arrival = new(2028, 1, 1), Departure = new(2028, 1, 2) },
            "adult" => input with { Rooms = [new GuestInput(0, [4], Guid.NewGuid())] },
            "age" => input with { Rooms = [new GuestInput(1, [18], Guid.NewGuid())] },
            "rooms" => input with { Rooms = Enumerable.Repeat<GuestInput?>(new GuestInput(1, [], Guid.NewGuid()), 9).ToArray() },
            _ => input with { Rooms = [null] }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/availability", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/quotes", input)).StatusCode);
    }

    [Fact]
    public async Task Quotes_sum_decimal_nights_multiroom_and_reject_combination_over_stock()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var id = await CreateInventory(2);
        var response = await client.PostAsJsonAsync("/api/quotes", Stay(id, 2));
        var quote = (await response.Content.ReadFromJsonAsync<QuoteView>())!;
        Assert.Equal(401m, quote.Total); Assert.Equal(2, quote.Nights); Assert.Equal(2, quote.Items.Count);
        Assert.Equal("CNY", quote.Currency); Assert.Equal(64, quote.PriceFingerprint.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", Stay(id, 3))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", Stay(id) with { Rooms = [new GuestInput(3, [1, 2], id)] })).StatusCode);
    }

    [Fact]
    public async Task Creation_is_owned_csrf_protected_and_requires_idempotency()
    {
        await using var factory = Factory(); using var visitor = factory.CreateClient(); using var client = await Client(factory);
        var input = await Payload(client, Stay(await CreateInventory()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Reserve(visitor, input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/reservations", input, Guid.NewGuid().ToString("N"), false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, "/reservations", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/reservations")).StatusCode);
    }

    [Fact]
    public async Task Atomic_multiroom_idempotency_ownership_cancellation_and_boundaries()
    {
        await using var factory = Factory(); using var client = await Client(factory); using var other = await Client(factory);
        var type = await CreateInventory(2); var input = await Payload(client, Stay(type, 2)); var key = Guid.NewGuid().ToString("N");
        var created = await Reserve(client, input, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = data.GetProperty("id").GetGuid();
        Assert.Equal(2, data.GetProperty("items").GetArrayLength()); Assert.Equal("PendingPayment", data.GetProperty("status").GetString());
        Assert.Equal(TimeSpan.FromMinutes(30), data.GetProperty("expiresAt").GetDateTimeOffset() - data.GetProperty("createdAt").GetDateTimeOffset());
        var repeat = await Reserve(client, input, key); Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(id, (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Reserve(client, input with { Locale = "en" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reservations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(other, $"/reservations/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, $"/reservations/{id}/cancel", new { }, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", Stay(type))).StatusCode);
        var adjacent = Stay(type) with { Arrival = input.Departure, Departure = input.Departure.AddDays(1) };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/quotes", adjacent)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/quotes", Stay(type, 2))).StatusCode);
        await using var db = postgres.OpenDbContext();
        Assert.Equal(1, await db.Reservations.CountAsync(x => x.Id == id));
        Assert.Equal(2, await db.ReservationItems.CountAsync(x => x.ReservationId == id));
    }

    [Fact]
    public async Task Concurrent_buyers_cannot_double_book_last_room_or_partially_allocate()
    {
        await using var factory = Factory(); using var first = await Client(factory); using var second = await Client(factory);
        var scarce = await CreateInventory(); var ample = await CreateInventory(2);
        var stay = Stay(scarce) with { Rooms = [new GuestInput(2, [], scarce), new GuestInput(2, [], ample)] };
        var input = await Payload(first, stay);
        var results = await Task.WhenAll(Reserve(first, input), Reserve(second, input));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = postgres.OpenDbContext();
        var roomIds = db.Rooms.Where(x => x.RoomTypeId == scarce || x.RoomTypeId == ample).Select(x => x.Id);
        Assert.Equal(2, await db.ReservationItems.CountAsync(x => roomIds.Contains(x.RoomId)));
    }

    [Fact]
    public async Task Simultaneous_duplicate_requests_recover_one_reservation()
    {
        await using var factory = Factory(); using var client = await Client(factory);
        var type = await CreateInventory(2); var input = await Payload(client, Stay(type)); var key = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Reserve(client, input, key), Reserve(client, input, key));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        var ids = await Task.WhenAll(responses.Select(async x => (await x.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()));
        Assert.Equal(ids[0], ids[1]);
    }

    [Fact]
    public async Task Price_change_requires_reacceptance_and_snapshot_survives_catalog_edits()
    {
        await using var factory = Factory(); using var client = await Client(factory); var type = await CreateInventory();
        var input = await Payload(client, Stay(type));
        await using var db = postgres.OpenDbContext();
        await db.RoomTypes.Where(x => x.Id == type).ExecuteUpdateAsync(s => s.SetProperty(x => x.NightlyRate, 125.55m));
        var changed = await Reserve(client, input);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var problem = await changed.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("PRICE_CHANGED", problem.GetProperty("code").GetString());
        Assert.Equal(251.10m, problem.GetProperty("quote").GetProperty("total").GetDecimal());
        var updated = input with { PriceFingerprint = problem.GetProperty("quote").GetProperty("priceFingerprint").GetString() };
        var result = await Reserve(client, updated); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var id = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await db.RoomTypes.Where(x => x.Id == type).ExecuteUpdateAsync(s => s.SetProperty(x => x.NameEs, "Nuevo nombre").SetProperty(x => x.NightlyRate, 999m));
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/reservations/{id}");
        Assert.Equal(251.10m, saved.GetProperty("total").GetDecimal()); Assert.Equal("Espacio original", saved.GetProperty("items")[0].GetProperty("nameEs").GetString());
    }

    [Fact]
    public async Task Expired_holds_are_immediately_ignored_before_periodic_persistence()
    {
        var clock = new Clock(); await using var factory = Factory(clock); using var client = await Client(factory);
        var type = await CreateInventory(); var input = await Payload(client, Stay(type));
        var response = await Reserve(client, input); var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        clock.Now = clock.Now.AddMinutes(30);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/reservations/{id}"); Assert.Equal("Expired", detail.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/quotes", Stay(type))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        await using var db = postgres.OpenDbContext(); await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db); await Inventory.ExpireAsync(db, clock.Now); await transaction.CommitAsync();
        Assert.Equal("Expired", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Reserve(client, input, (await db.IdempotencyRequests.SingleAsync(x => x.ResourceId == id)).Key)).StatusCode);
    }

    [Fact]
    public async Task Arrival_day_cannot_cancel_and_confirmed_inventory_remains_blocked()
    {
        var clock = new Clock { Now = new DateTimeOffset(2026, 11, 9, 16, 0, 0, TimeSpan.Zero) }; await using var factory = Factory(clock); using var client = await Client(factory);
        var type = await CreateInventory(); var input = await Payload(client, Stay(type));
        var id = (await (await Reserve(client, input)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var db = postgres.OpenDbContext();
        await db.Reservations.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, clock.Now.AddDays(20)));
        clock.Now = new DateTimeOffset(2026, 11, 9, 16, 0, 0, TimeSpan.Zero);
        var closed = await Write(client, $"/reservations/{id}/cancel", new { }); Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("CANCELLATION_CLOSED", (await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await db.Reservations.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Confirmed").SetProperty(x => x.ExpiresAt, clock.Now.AddDays(-1)));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/quotes", Stay(type))).StatusCode);
    }

    [Fact]
    public async Task Catalog_cannot_disable_or_renumber_committed_inventory_but_can_edit_rates()
    {
        await using var factory = Factory(); using var client = await Client(factory); using var admin = await Client(factory, true);
        var typeId = await CreateInventory(); var input = await Payload(client, Stay(typeId));
        var id = (await (await Reserve(client, input)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var db = postgres.OpenDbContext(); var room = await db.Rooms.SingleAsync(x => x.RoomTypeId == typeId);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, $"/admin/rooms/{room.Id}", new RoomInput(typeId, room.Number, false, room.Revision), method: HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, $"/admin/rooms/{room.Id}", new RoomInput(typeId, "RENAMED", true, room.Revision), method: HttpMethod.Put)).StatusCode);
        var type = await db.RoomTypes.SingleAsync(x => x.Id == typeId);
        var payload = new RoomTypeInput(type.Slug, type.NameEs, type.NameEn, "Descripción", "Description", "room", "king", 60, false, 4, 3, 2, 150m, false, 0, type.Revision, [new ImageInput("/images/regent/classic.webp", "Dormitorio", "Bedroom")]);
        Assert.Equal(HttpStatusCode.Conflict, (await Write(admin, $"/admin/room-types/{typeId}", payload, method: HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(admin, $"/admin/room-types/{typeId}", payload with { Active = true }, method: HttpMethod.Put)).StatusCode);
        await Write(client, $"/reservations/{id}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, (await Write(admin, $"/admin/rooms/{room.Id}", new RoomInput(typeId, room.Number, false, room.Revision), method: HttpMethod.Put)).StatusCode);
    }

    [Fact]
    public async Task Lists_are_owned_paginated_and_admin_can_filter_without_mutating()
    {
        await using var factory = Factory(); using var client = await Client(factory); using var other = await Client(factory); using var admin = await Client(factory, true);
        var input = await Payload(client, Stay(await CreateInventory())); var response = await Reserve(client, input);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var own = await client.GetFromJsonAsync<JsonElement>("/api/reservations?pageSize=1"); Assert.Equal(1, own.GetProperty("total").GetInt32());
        var none = await other.GetFromJsonAsync<JsonElement>("/api/reservations"); Assert.Equal(0, none.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/reservations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/reservations?status=PendingPayment")).StatusCode);
        var future = await admin.GetFromJsonAsync<JsonElement>("/api/admin/reservations?arrivalFrom=2028-01-01"); Assert.Equal(0, future.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/reservations?arrivalFrom=2027-01-01&arrivalTo=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/reservations?status=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reservations?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(admin, $"/reservations/{id}/cancel", new { })).StatusCode);
    }
}
