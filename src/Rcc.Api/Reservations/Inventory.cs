using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;

namespace Rcc.Api.Reservations;

public static class Inventory
{
    // Single-hotel application: serialize inventory mutations, then lock candidate rooms
    // in UUID order. Catalog writes follow this same gate, so capacity/status cannot race.
    // Every caller owns a transaction; no session-level or in-memory locks.
    public static async Task LockAsync(RccDbContext db) =>
        _ = await db.Hotels.FromSqlInterpolated($"SELECT * FROM \"Hotels\" WHERE \"Id\" = {CatalogSeed.HotelId} FOR UPDATE").AsNoTracking().ToListAsync();

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")).DateTime);

    public static IQueryable<ReservationItem> Committed(RccDbContext db, DateTimeOffset now) =>
        db.ReservationItems.Where(x => x.Reservation.Status == "Confirmed" ||
            (x.Reservation.Status == "PendingPayment" && x.Reservation.ExpiresAt > now));

    public static Task<bool> HasFutureAsync(RccDbContext db, Guid? roomId, Guid? typeId, TimeProvider clock)
    {
        var now = clock.GetUtcNow(); var today = Today(clock);
        var rooms = db.Rooms.Where(x => roomId == null || x.Id == roomId).Where(x => typeId == null || x.RoomTypeId == typeId).Select(x => x.Id);
        return Committed(db, now).AnyAsync(x => rooms.Contains(x.RoomId) && x.Reservation.Departure > today);
    }

    public static Task<int> ExpireAsync(RccDbContext db, DateTimeOffset now) => db.Reservations
        .Where(x => x.Status == "PendingPayment" && x.ExpiresAt <= now)
        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Expired"));
}

public sealed class ReservationExpiry(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ReservationExpiry> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<RccDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                await Inventory.LockAsync(db);
                await Inventory.ExpireAsync(db, clock.GetUtcNow());
                await transaction.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Reservation expiry failed ({ErrorType}); retrying next minute.", error.GetType().Name); }
        }
    }
}
