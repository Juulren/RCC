using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;
using Rcc.Api.Reservations;

namespace Rcc.Api.Payments;

public sealed class PaymentProcessor(RccDbContext db, IStripeGateway stripe, TimeProvider clock)
{
    // All state transitions share Inventory.LockAsync. Provider calls are outside
    // transactions; snapshots can arrive out of order and must never regress money.
    public async Task<string> ApplyAsync(Guid paymentId, StripeSession snapshot, string? eventId = null, string eventType = "reconciliation")
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        if (eventId is not null && await db.PaymentEvents.AnyAsync(x => x.Id == eventId)) return "Duplicate";
        var payment = await db.Payments.Include(x => x.Reservation).Include(x => x.Refund).SingleAsync(x => x.Id == paymentId);
        // A preceding operation in this scope may have loaded these rows.
        var now = clock.GetUtcNow();
        var outcome = "Processed";
        var binding = !snapshot.Live && snapshot.Id.StartsWith("cs_test_", StringComparison.Ordinal) && snapshot.Mode == "payment" && snapshot.Amount == payment.AmountMinor && snapshot.Currency == payment.Currency && snapshot.ReservationId == payment.ReservationId.ToString() && snapshot.ClientReferenceId == payment.ReservationId.ToString() && snapshot.PaymentId == payment.Id.ToString() && (payment.SessionId is null || payment.SessionId == snapshot.Id);
        if (!binding || (snapshot.PaymentStatus == "paid" && (snapshot.IntentId is null || snapshot.IntentLive || snapshot.IntentStatus != "succeeded" || snapshot.IntentReceived != payment.AmountMinor || snapshot.IntentCurrency != payment.Currency || (payment.PaymentIntentId is not null && payment.PaymentIntentId != snapshot.IntentId))))
        {
            payment.LastError = "PAYMENT_VERIFICATION_FAILED";
            if (payment.State is not ("Paid" or "Refunded")) payment.State = "Review";
            outcome = "Rejected";
        }
        else
        {
            payment.SessionId = snapshot.Id;
            if (snapshot.PaymentStatus == "paid")
            {
                payment.PaymentIntentId = snapshot.IntentId;
                if (payment.State != "Refunded") payment.State = "Paid";
                var reservation = payment.Reservation;
                if (reservation.Status == "PendingPayment" && reservation.ExpiresAt > now && payment.Refund is null)
                {
                    reservation.Status = "Confirmed";
                    await Rcc.Api.Services.NotificationQueue.EnqueueAsync(db, reservation, "Confirmed", now);
                }
                else if (reservation.Status != "Confirmed")
                {
                    if (reservation.Status == "PendingPayment") reservation.Status = "Expired";
                    var refund = QueueRefund(payment, now);
                    if (refund is not null) db.RefundJobs.Add(refund);
                }
            }
            else if (payment.State is not ("Paid" or "Refunded" or "Review"))
                payment.State = snapshot.Status == "expired" ? "Expired" : "Open";
            if (snapshot.Status == "expired" && payment.State == "Expired" && payment.Reservation.Status == "PendingPayment") payment.Reservation.Status = "Expired";
            if (payment.State != "Review") payment.LastError = null;
        }
        payment.SyncAttempts++;
        // Reconcile refunded/expired sessions too for 24h: a delayed settlement can
        // arrive after the local hold, and must produce a refund rather than a stay.
        payment.NextSyncAt = now.AddMinutes(payment.State is "Paid" or "Refunded" ? 60 : 1);
        if (eventId is not null) db.PaymentEvents.Add(new PaymentEvent { Id = eventId, Type = eventType, PaymentId = paymentId, ReceivedAt = now, Outcome = outcome });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return outcome;
    }

    public static RefundJob? QueueRefund(Payment payment, DateTimeOffset now)
    {
        if (payment.State != "Paid" || payment.PaymentIntentId is null || payment.Refund is not null) return null;
        payment.Refund = new RefundJob { Id = Guid.NewGuid(), PaymentId = payment.Id, AmountMinor = payment.AmountMinor, CreatedAt = now, NextAttemptAt = now };
        return payment.Refund;
    }

    public async Task<StripeSession> SyncAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().Include(x => x.Reservation).SingleAsync(x => x.Id == id, cancellationToken);
        if (payment.SessionId is null && clock.GetUtcNow() - payment.CreatedAt > TimeSpan.FromHours(23))
        {
            await RecordError(id, "CHECKOUT_REQUIRES_REVIEW");
            throw new InvalidOperationException("CHECKOUT_REQUIRES_REVIEW");
        }
        var snapshot = payment.SessionId is null ? await stripe.CreateAsync(payment, cancellationToken) : await stripe.GetAsync(payment.SessionId, cancellationToken);
        await ApplyAsync(id, snapshot);
        db.ChangeTracker.Clear();
        var current = await db.Payments.AsNoTracking().Include(x => x.Reservation).SingleAsync(x => x.Id == id, cancellationToken);
        if (snapshot.Status == "open" && current.State == "Open" && current.SessionId == snapshot.Id && (current.Reservation.Status != "PendingPayment" || current.Reservation.ExpiresAt <= clock.GetUtcNow()))
        {
            // Cancellation/expiry can win during the provider request. Never return
            // the stale URL, and attempt remote expiry without extending inventory.
            await stripe.ExpireAsync(snapshot.Id, cancellationToken);
        }
        return snapshot;
    }

    public async Task RecordError(Guid id, string code)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Inventory.LockAsync(db);
        var payment = await db.Payments.SingleAsync(x => x.Id == id);
        if (payment.State == "Review" && code == "PAYMENT_PROVIDER_UNAVAILABLE") return;
        payment.LastError = code; payment.SyncAttempts++;
        payment.NextSyncAt = clock.GetUtcNow().AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(payment.SyncAttempts, 7))));
        if (code == "CHECKOUT_REQUIRES_REVIEW") payment.State = "Review";
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public async Task RefundOneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.Clear();
        await using (var claim = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await Inventory.LockAsync(db);
            var job = await db.RefundJobs.SingleAsync(x => x.Id == id, cancellationToken);
            var now = clock.GetUtcNow();
            if (job.State is "Succeeded" or "Failed" or "Review" || job.NextAttemptAt > now || job.LeaseUntil > now) return;
            if (job.StripeRefundId is null && now - job.CreatedAt > TimeSpan.FromHours(23))
            {
                job.State = "Review"; job.LastError = "REFUND_REQUIRES_REVIEW";
                await db.SaveChangesAsync(cancellationToken); await claim.CommitAsync(cancellationToken); return;
            }
            job.LeaseUntil = now.AddMinutes(2); job.Attempts++;
            await db.SaveChangesAsync(cancellationToken); await claim.CommitAsync(cancellationToken);
        }
        var record = await db.RefundJobs.AsNoTracking().Include(x => x.Payment).SingleAsync(x => x.Id == id, cancellationToken);
        StripeRefund? result = null;
        try
        {
            result = record.StripeRefundId is null ? await stripe.RefundAsync(record.Payment, record, cancellationToken) : await stripe.GetRefundAsync(record.StripeRefundId, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { /* Durable job, sanitized retry below. */ }
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await Inventory.LockAsync(db);
        var saved = await db.RefundJobs.Include(x => x.Payment).SingleAsync(x => x.Id == id, cancellationToken);
        if (saved.State is "Succeeded" or "Failed" or "Review") return;
        saved.LeaseUntil = null;
        if (result is null) { saved.LastError = "REFUND_PROVIDER_UNAVAILABLE"; saved.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(saved.Attempts, 7)))); }
        else if (result.Live || result.Amount != saved.AmountMinor || result.Currency != saved.Payment.Currency || result.IntentId != saved.Payment.PaymentIntentId || (saved.StripeRefundId is not null && result.Id != saved.StripeRefundId)) { saved.State = "Review"; saved.LastError = "REFUND_VERIFICATION_FAILED"; }
        else
        {
            saved.StripeRefundId = result.Id;
            saved.State = result.Status switch { "succeeded" => "Succeeded", "failed" or "canceled" => "Failed", _ => "Processing" };
            saved.LastError = saved.State == "Failed" ? "REFUND_FAILED" : null;
            saved.NextAttemptAt = clock.GetUtcNow().AddMinutes(1);
            if (saved.State == "Succeeded") saved.Payment.State = "Refunded";
        }
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}
