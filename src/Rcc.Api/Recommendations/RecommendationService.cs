using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Reservations;
namespace Rcc.Api.Recommendations;

public sealed record RecommendationInput(DateOnly Arrival, DateOnly Departure, GuestInput?[]? Rooms, decimal? Budget, string[]? Preferences, string? Locale);
public sealed record RecommendedStay(string Id, QuoteView Quote, string Explanation);
public sealed record RecommendationResult(string Mode, string Reason, List<RecommendedStay> Options, bool IsDemo = true, string Currency = "CNY");
public sealed class AiDailyUsage
{
    public DateOnly Day { get; set; }
    public int Requests { get; set; }
}
public sealed class RecommendationService(RccDbContext db, IRecommendationGateway gateway, IOptions<AiSettings> settings, TimeProvider clock)
{
    public static Dictionary<string, string[]> Validate(RecommendationInput input, TimeProvider clock)
    {
        var errors = ReservationEndpoints.Validate(new(input.Arrival, input.Departure, input.Rooms), clock, false);
        if (input.Locale is not ("es" or "en")) errors["locale"] = ["Use es or en."];
        if (input.Budget is <= 0 or > 100000000 || input.Budget.HasValue && decimal.Round(input.Budget.Value, 2) != input.Budget) errors["budget"] = ["Budget is a positive total in CNY with at most two decimals."];
        if (input.Preferences is null || input.Preferences.Length > 3 || input.Preferences.Distinct().Count() != input.Preferences.Length || input.Preferences.Any(x => x is not ("river" or "space" or "value"))) errors["preferences"] = ["Use distinct river, space or value preferences."];
        return errors;
    }
    public async Task<RecommendationResult> RecommendAsync(RecommendationInput input, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var busy = Inventory.Committed(db, now).Where(x => x.Reservation.Arrival < input.Departure && x.Reservation.Departure > input.Arrival).Select(x => x.RoomId);
        var types = await db.RoomTypes.AsNoTracking().Where(x => x.Active && x.HotelId == CatalogSeed.HotelId).Select(x => new
        {
            x.Id,
            x.NameEs,
            x.NameEn,
            x.NightlyRate,
            x.Capacity,
            x.MaxAdults,
            x.MaxChildren,
            x.RiverView,
            x.Area,
            Count = x.Rooms.Count(room => room.Active && !busy.Contains(room.Id))
        }).Where(x => x.Count > 0).ToListAsync(token);
        var nights = input.Departure.DayNumber - input.Arrival.DayNumber;
        var states = new List<Candidate> { new("", [], 0, 0, 0, []) };
        var preferences = input.Preferences!;
        foreach (var guest in input.Rooms!)
        {
            var expanded = new List<Candidate>();
            foreach (var state in states)
                foreach (var type in types)
                {
                    if (guest!.Adults > type.MaxAdults || guest.ChildAges!.Length > type.MaxChildren || guest.Adults + guest.ChildAges.Length > type.Capacity || state.RoomTypeIds.Count(x => x == type.Id) >= type.Count) continue;
                    var total = state.Total + type.NightlyRate * nights;
                    if (input.Budget.HasValue && total > input.Budget) continue;
                    expanded.Add(new("", [.. state.RoomTypeIds, type.Id], total, state.RiverRooms + (type.RiverView ? 1 : 0), state.Area + type.Area, [.. state.Names, input.Locale == "es" ? type.NameEs : type.NameEn]));
                }
            // Bounded search: retain cheapest paths as well as preference-ranked paths.
            // A bounded search can omit a feasible combination; it never invents inventory.
            states = expanded.OrderBy(x => x.Total).ThenBy(x => string.Join(',', x.RoomTypeIds)).Take(24)
                .Concat(expanded.OrderByDescending(x => Score(x, preferences)).ThenBy(x => x.Total).ThenBy(x => string.Join(',', x.RoomTypeIds)).Take(24))
                .DistinctBy(x => string.Join(',', x.RoomTypeIds)).ToList();
            if (states.Count == 0) return new("deterministic", "NO_MATCH", []);
        }
        var candidates = states.OrderByDescending(x => Score(x, preferences)).ThenBy(x => x.Total).ThenBy(x => string.Join(',', x.RoomTypeIds)).Take(12).Select((x, index) => x with { Id = "option-" + index }).ToList();
        if (candidates.Count == 0) return new("deterministic", "NO_MATCH", []);
        List<RankedChoice>? ranked = null;
        var reason = "AI_NOT_CONFIGURED";
        if (settings.Value.Ready)
        {
            var day = DateOnly.FromDateTime(now.UtcDateTime);
            var limit = settings.Value.DailyRequestLimit;
            // Atomic durable project-wide request budget, shared across instances/restarts.
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AiDailyUsages\" (\"Day\", \"Requests\") VALUES ({day}, 1) ON CONFLICT (\"Day\") DO UPDATE SET \"Requests\" = \"AiDailyUsages\".\"Requests\" + 1 WHERE \"AiDailyUsages\".\"Requests\" < {limit}", token);
            reason = "AI_DAILY_LIMIT";
            if (claimed == 1)
            {
                reason = "AI_UNAVAILABLE";
                try
                {
                    var result = await gateway.RankAsync(candidates, preferences, input.Locale!, token);
                    if (ValidRanking(result, candidates)) { ranked = result; reason = "AI_RANKED"; }
                    else reason = "AI_INVALID_RESPONSE";
                }
                catch (System.Net.Http.HttpRequestException error)
                {
                    // Only a fixed diagnostic code leaves the server, never a provider body or credential.
                    reason = error.StatusCode switch
                    {
                        System.Net.HttpStatusCode.TooManyRequests => "AI_PROVIDER_QUOTA",
                        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "AI_PROVIDER_AUTH",
                        System.Net.HttpStatusCode.NotFound => "AI_MODEL_UNAVAILABLE",
                        System.Net.HttpStatusCode.BadRequest => "AI_PROVIDER_REQUEST",
                        _ => "AI_UNAVAILABLE"
                    };
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { reason = "AI_PROVIDER_TIMEOUT"; }
                catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { reason = "AI_INVALID_RESPONSE"; }
            }
        }
        var choices = ranked ?? candidates.Take(3).Select(x => new RankedChoice(x.Id, input.Locale == "es" ? "Opción calculada por capacidad, preferencias y precio. No intervino IA." : "Option calculated by capacity, preferences and price. No AI was used.")).ToList();
        var options = new List<RecommendedStay>();
        foreach (var choice in choices)
        {
            var candidate = candidates.Single(x => x.Id == choice.Id);
            var selected = input.Rooms!.Select((x, i) => x! with { RoomTypeId = candidate.RoomTypeIds[i] }).ToArray();
            var checkedQuote = await ReservationEndpoints.Calculate(new(input.Arrival, input.Departure, selected), db, clock);
            if (checkedQuote.Error is not null || checkedQuote.Quote!.Total != candidate.Total || input.Budget.HasValue && checkedQuote.Quote.Total > input.Budget) continue;
            options.Add(new(candidate.Id, checkedQuote.Quote, choice.Explanation));
        }
        return new(ranked is null ? "deterministic" : "ai", options.Count == 0 ? "AVAILABILITY_CHANGED" : reason, options);
    }
    private static decimal Score(Candidate candidate, string[] preferences) =>
        (preferences.Contains("river") ? candidate.RiverRooms * 100000m : 0) + (preferences.Contains("space") ? candidate.Area * 100m : 0) - (preferences.Contains("value") || preferences.Length == 0 ? candidate.Total : 0);
    public static bool ValidRanking(List<RankedChoice>? choices, List<Candidate> candidates) => choices is { Count: >= 1 and <= 3 } && choices.Select(x => x.Id).Distinct().Count() == choices.Count && choices.All(x => candidates.Any(c => c.Id == x.Id) && !string.IsNullOrWhiteSpace(x.Explanation) && x.Explanation.Length <= 600 && !x.Explanation.Any(c => char.IsControl(c)));
}
