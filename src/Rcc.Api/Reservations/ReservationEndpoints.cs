using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rcc.Api.Catalog;
using Rcc.Api.Data;
using Rcc.Api.Payments;

namespace Rcc.Api.Reservations;

public static class ReservationEndpoints
{
    public static void MapRccReservations(this WebApplication app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        api.MapPost("/availability", Availability);
        api.MapPost("/quotes", Quote);
        var owned = api.MapGroup("/reservations").RequireAuthorization(policy => policy.RequireRole("Customer", "Admin")).AddEndpointFilter<CatalogWriteFilter>();
        owned.MapPost("", Create);
        owned.MapGet("", (HttpContext context, RccDbContext db, TimeProvider clock, int page = 1, int pageSize = 20) => List(db, clock, context.User.FindFirstValue(ClaimTypes.NameIdentifier), page, pageSize, null));
        owned.MapGet("/{id:guid}", (Guid id, HttpContext context, RccDbContext db, TimeProvider clock) => Detail(id, context.User.FindFirstValue(ClaimTypes.NameIdentifier), db, clock));
        owned.MapPost("/{id:guid}/cancel", Cancel);
        var admin = api.MapGroup("/admin/reservations").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapGet("", (RccDbContext db, TimeProvider clock, int page = 1, int pageSize = 20, string? status = null, DateOnly? arrivalFrom = null, DateOnly? arrivalTo = null) => List(db, clock, null, page, pageSize, status, arrivalFrom, arrivalTo));
        admin.MapGet("/{id:guid}", (Guid id, RccDbContext db, TimeProvider clock) => Detail(id, null, db, clock));
    }

    private static Dictionary<string, string[]> Validate(StayInput input, TimeProvider clock, bool selected)
    {
        var errors = new Dictionary<string, string[]>();
        var today = Inventory.Today(clock);
        if (input.Arrival < today || input.Arrival > today.AddDays(365)) errors["arrival"] = ["Arrival must be within the next 365 days in Chongqing."];
        var nights = input.Departure.DayNumber - input.Arrival.DayNumber;
        if (nights is < 1 or > 30) errors["departure"] = ["A stay must be between 1 and 30 nights."];
        if (input.Rooms is null || input.Rooms.Length is < 1 or > 8) errors["rooms"] = ["Select between 1 and 8 rooms."];
        else for (var i = 0; i < input.Rooms.Length; i++)
        {
            var guest = input.Rooms[i];
            if (guest is null || guest.Adults is < 1 or > 12 || guest.ChildAges is null || guest.ChildAges.Length > 11 || guest.ChildAges.Any(age => age is < 0 or > 17) || guest.Adults + guest.ChildAges.Length > 12 || (selected && (guest.RoomTypeId is null || guest.RoomTypeId == Guid.Empty)))
                errors[$"rooms[{i}]"] = ["Each room needs an adult, children aged 0–17 and a valid selection (maximum 12 guests)."];
        }
        return errors;
    }

    private static async Task<IResult> Availability(StayInput input, RccDbContext db, TimeProvider clock)
    {
        var errors = Validate(input, clock, false);
        if (errors.Count > 0) return Invalid(errors);
        var now = clock.GetUtcNow();
        var busy = Inventory.Committed(db, now).Where(x => x.Reservation.Arrival < input.Departure && x.Reservation.Departure > input.Arrival).Select(x => x.RoomId);
        var types = await db.RoomTypes.AsNoTracking().Where(x => x.HotelId == CatalogSeed.HotelId && x.Active)
            .OrderBy(x => x.NightlyRate).ThenBy(x => x.Id).Select(x => new
            {
                roomType = new { x.Id, x.NameEs, x.NameEn, x.Capacity, x.MaxAdults, x.MaxChildren, x.NightlyRate, x.Area,
                    image = x.Images.OrderBy(image => image.SortOrder).Select(image => new { image.Url, image.AltEs, image.AltEn }).FirstOrDefault() },
                availableCount = x.Rooms.Count(room => room.Active && !busy.Contains(room.Id))
            }).ToListAsync();
        return Results.Ok(new { input.Arrival, input.Departure, timeZone = "Asia/Shanghai", today = Inventory.Today(clock), currency = "CNY", isDemo = true,
            selections = input.Rooms!.Select((guest, index) => new { index, options = types.Where(x => x.availableCount > 0 && guest!.Adults <= x.roomType.MaxAdults && guest.ChildAges!.Length <= x.roomType.MaxChildren && guest.Adults + guest.ChildAges.Length <= x.roomType.Capacity) }) });
    }

    private static async Task<(QuoteView? Quote, string? Error, List<PhysicalRoom> Rooms)> Calculate(StayInput input, RccDbContext db, TimeProvider clock)
    {
        var ids = input.Rooms!.Select(x => x!.RoomTypeId!.Value).Distinct().ToArray();
        var types = await db.RoomTypes.AsNoTracking().Where(x => ids.Contains(x.Id) && x.HotelId == CatalogSeed.HotelId && x.Active).ToDictionaryAsync(x => x.Id);
        var now = clock.GetUtcNow();
        var busy = Inventory.Committed(db, now).Where(x => x.Reservation.Arrival < input.Departure && x.Reservation.Departure > input.Arrival).Select(x => x.RoomId);
        var candidates = await db.Rooms.AsNoTracking().Where(x => x.HotelId == CatalogSeed.HotelId && x.Active && ids.Contains(x.RoomTypeId) && !busy.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
        var lines = new List<QuoteLine>(); var assigned = new List<PhysicalRoom>();
        var nights = input.Departure.DayNumber - input.Arrival.DayNumber;
        foreach (var guest in input.Rooms!)
        {
            if (!types.TryGetValue(guest!.RoomTypeId!.Value, out var type)) return (null, "ROOM_UNAVAILABLE", []);
            if (guest.Adults > type.MaxAdults || guest.ChildAges!.Length > type.MaxChildren || guest.Adults + guest.ChildAges.Length > type.Capacity) return (null, "CAPACITY_EXCEEDED", []);
            var room = candidates.FirstOrDefault(x => x.RoomTypeId == type.Id);
            if (room is null) return (null, "ROOM_UNAVAILABLE", []);
            candidates.Remove(room); assigned.Add(room);
            lines.Add(new(type.Id, type.NameEs, type.NameEn, guest.Adults, guest.ChildAges!, type.NightlyRate, type.NightlyRate * nights));
        }
        var fingerprint = Hash(JsonSerializer.Serialize(new { input.Arrival, input.Departure, lines = lines.Select(x => new { x.RoomTypeId, x.Adults, x.ChildAges, rate = x.NightlyRate.ToString("0.00", CultureInfo.InvariantCulture) }) }));
        return (new(input.Arrival, input.Departure, nights, lines, lines.Sum(x => x.Subtotal), fingerprint), null, assigned);
    }

    private static async Task<IResult> Quote(StayInput input, RccDbContext db, TimeProvider clock)
    {
        var errors = Validate(input, clock, true);
        if (errors.Count > 0) return Invalid(errors);
        var result = await Calculate(input, db, clock);
        return result.Error is null ? Results.Ok(result.Quote) : CatalogEndpoints.Error(409, result.Error);
    }

    private static async Task<IResult> Create(ReserveInput input, HttpContext context, RccDbContext db, TimeProvider clock)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 16 or > 100 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return CatalogEndpoints.Error(400, "IDEMPOTENCY_KEY_REQUIRED");
        if (input.Locale is not ("es" or "en") || input.PriceFingerprint?.Length != 64) return CatalogEndpoints.Error(400, "INVALID_RESERVATION");
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var hash = Hash(JsonSerializer.Serialize(input));
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        var previous = await db.IdempotencyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user && x.Operation == "reservation" && x.Key == key);
        if (previous is not null)
        {
            if (previous.RequestHash != hash) return CatalogEndpoints.Error(409, "IDEMPOTENCY_CONFLICT");
            return await Detail(previous.ResourceId, user, db, clock);
        }
        var stay = new StayInput(input.Arrival, input.Departure, input.Rooms);
        var errors = Validate(stay, clock, true);
        if (errors.Count > 0) return Invalid(errors);
        // Lock all candidates in stable order before the overlap query, including rooms
        // currently occupied. READ COMMITTED sees preceding committed allocations.
        var ids = input.Rooms!.Select(x => x!.RoomTypeId!.Value).Distinct().ToArray();
        _ = await db.Rooms.FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"RoomTypeId\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE").AsNoTracking().ToListAsync();
        var calculated = await Calculate(stay, db, clock);
        if (calculated.Error is not null) return CatalogEndpoints.Error(409, calculated.Error);
        var quote = calculated.Quote!;
        if (!string.Equals(input.PriceFingerprint, quote.PriceFingerprint, StringComparison.Ordinal))
            return Results.Problem(statusCode: 409, title: "PRICE_CHANGED", extensions: new Dictionary<string, object?> { ["code"] = "PRICE_CHANGED", ["quote"] = quote });
        var now = clock.GetUtcNow();
        var reservation = new Reservation { Id = Guid.NewGuid(), UserId = user, Arrival = input.Arrival, Departure = input.Departure, CreatedAt = now, ExpiresAt = now.AddMinutes(30), Total = quote.Total, Locale = input.Locale! };
        reservation.Items = quote.Items.Select((line, index) => new ReservationItem { Id = Guid.NewGuid(), ReservationId = reservation.Id, RoomId = calculated.Rooms[index].Id, NameEs = line.NameEs, NameEn = line.NameEn, NightlyRate = line.NightlyRate, Adults = line.Adults, ChildAges = line.ChildAges, Subtotal = line.Subtotal }).ToList();
        db.Reservations.Add(reservation);
        db.IdempotencyRequests.Add(new IdempotencyRequest { Id = Guid.NewGuid(), UserId = user, Operation = "reservation", Key = key, RequestHash = hash, ResourceId = reservation.Id, CreatedAt = now });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/reservations/{reservation.Id}", View(reservation, clock));
    }

    private static async Task<IResult> Cancel(Guid id, HttpContext context, RccDbContext db, TimeProvider clock)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reservation = await db.Reservations.Include(x => x.Items).Include(x => x.Payments).ThenInclude(x => x.Refund).SingleOrDefaultAsync(x => x.Id == id && x.UserId == user);
        if (reservation is null) return Results.NotFound();
        if (reservation.Status == "Cancelled") return Results.Ok(View(reservation, clock));
        if (EffectiveStatus(reservation, clock.GetUtcNow()) == "Expired") return CatalogEndpoints.Error(409, "RESERVATION_EXPIRED");
        if (reservation.Arrival <= Inventory.Today(clock)) return CatalogEndpoints.Error(409, "CANCELLATION_CLOSED");
        var payment = reservation.Payments.SingleOrDefault();
        if (reservation.Status == "Confirmed" && (payment?.State != "Paid" || payment.PaymentIntentId is null)) return CatalogEndpoints.Error(409, "PAYMENT_REQUIRES_REVIEW");
        reservation.Status = "Cancelled";
        if (payment is not null)
        {
            var refund = PaymentProcessor.QueueRefund(payment, clock.GetUtcNow());
            if (refund is not null) db.RefundJobs.Add(refund);
            payment.NextSyncAt = clock.GetUtcNow();
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Results.Ok(View(reservation, clock));
    }

    private static async Task<IResult> Detail(Guid id, string? user, RccDbContext db, TimeProvider clock)
    {
        var reservation = await db.Reservations.AsNoTracking().Include(x => x.Items).Include(x => x.Payments).ThenInclude(x => x.Refund).SingleOrDefaultAsync(x => x.Id == id && (user == null || x.UserId == user));
        return reservation is null ? Results.NotFound() : Results.Ok(View(reservation, clock));
    }

    private static async Task<IResult> List(RccDbContext db, TimeProvider clock, string? user, int page, int pageSize, string? status, DateOnly? arrivalFrom = null, DateOnly? arrivalTo = null)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100 || status is not (null or "PendingPayment" or "Confirmed" or "Expired" or "Cancelled")) return CatalogEndpoints.Error(400, "INVALID_FILTER");
        if (arrivalFrom.HasValue && arrivalTo.HasValue && arrivalFrom > arrivalTo) return CatalogEndpoints.Error(400, "INVALID_FILTER");
        var now = clock.GetUtcNow();
        var query = db.Reservations.AsNoTracking().Where(x => user == null || x.UserId == user);
        if (arrivalFrom.HasValue) query = query.Where(x => x.Arrival >= arrivalFrom.Value);
        if (arrivalTo.HasValue) query = query.Where(x => x.Arrival <= arrivalTo.Value);
        if (status is not null) query = status == "Expired"
            ? query.Where(x => x.Status == "Expired" || (x.Status == "PendingPayment" && x.ExpiresAt <= now))
            : status == "PendingPayment" ? query.Where(x => x.Status == status && x.ExpiresAt > now) : query.Where(x => x.Status == status);
        var total = await query.CountAsync();
        var records = await query.Include(x => x.Items).Include(x => x.Payments).ThenInclude(x => x.Refund).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Results.Ok(new { items = records.Select(x => View(x, clock)), page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) });
    }

    private static string EffectiveStatus(Reservation reservation, DateTimeOffset now) => reservation.Status == "PendingPayment" && reservation.ExpiresAt <= now ? "Expired" : reservation.Status;
    private static object View(Reservation reservation, TimeProvider clock) => new
    {
        reservation.Id, reservation.Arrival, reservation.Departure, status = EffectiveStatus(reservation, clock.GetUtcNow()), reservation.CreatedAt, reservation.ExpiresAt,
        reservation.Total, reservation.Currency, reservation.Locale, isDemo = true, serverNow = clock.GetUtcNow(), timeZone = "Asia/Shanghai",
        canCancel = EffectiveStatus(reservation, clock.GetUtcNow()) is "PendingPayment" or "Confirmed" && reservation.Arrival > Inventory.Today(clock),
        payments = reservation.Payments.Select(x => new { state = x.State, amount = x.AmountMinor / 100m, currency = "CNY", x.LastError, x.NextSyncAt,
            refund = x.Refund is null ? null : new { state = x.Refund.State, amount = x.Refund.AmountMinor / 100m, x.Refund.LastError, x.Refund.NextAttemptAt } }),
        items = reservation.Items.OrderBy(x => x.Id).Select(x => new { x.Id, x.NameEs, x.NameEn, x.NightlyRate, x.Adults, x.ChildAges, x.Subtotal })
    };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static IResult Invalid(Dictionary<string, string[]> errors) => Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_FAILED" });
}
