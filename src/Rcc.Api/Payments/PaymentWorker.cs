using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;

namespace Rcc.Api.Payments;

public sealed class PaymentWorker(IServiceScopeFactory scopes, IOptions<StripeSettings> settings, TimeProvider clock, ILogger<PaymentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!settings.Value.Ready) continue;
            try { await RunOnceAsync(scopes, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Payment reconciliation failed ({ErrorType}); retrying next cycle.", error.GetType().Name); }
        }
    }
    public static async Task RunOnceAsync(IServiceScopeFactory scopes, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RccDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<PaymentProcessor>();
        var now = clock.GetUtcNow();
        var payments = await db.Payments.AsNoTracking().Where(x => x.NextSyncAt <= now && x.State != "Review" && (x.CreatedAt > now.AddDays(-1) || x.State == "Creating" || (x.State == "Open" && x.Reservation.Status != "Confirmed")))
            .OrderBy(x => x.NextSyncAt).Take(20).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var id in payments)
        {
            try { await processor.SyncAsync(id, cancellationToken); }
            catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { await processor.RecordError(id, "PAYMENT_PROVIDER_UNAVAILABLE"); }
        }
        db.ChangeTracker.Clear();
        var jobs = await db.RefundJobs.AsNoTracking().Where(x => (x.State == "Pending" || x.State == "Processing") && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now)).OrderBy(x => x.NextAttemptAt).Take(20).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var id in jobs) await processor.RefundOneAsync(id, cancellationToken);
    }
}
