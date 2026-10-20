using System.Collections.Concurrent;
using Rcc.Api.Data;
using Rcc.Api.Payments;

namespace Rcc.Testing;

// Test-only provider. This source is linked by test projects, never the API.
public sealed class FakeStripeGateway : IStripeGateway
{
    public ConcurrentDictionary<string, StripeSession> Sessions { get; } = new();
    public ConcurrentDictionary<string, StripeRefund> Refunds { get; } = new();
    public bool FailReads { get; set; }
    public bool FailRefunds { get; set; }
    public bool LoseCreateResponse { get; set; }
    public bool LoseRefundResponse { get; set; }
    public string RefundStatus { get; set; } = "succeeded";
    public TaskCompletionSource? ReadStarted { get; set; }
    public TaskCompletionSource? ResumeRead { get; set; }
    public TaskCompletionSource? CreateStarted { get; set; }
    public TaskCompletionSource? ResumeCreate { get; set; }
    public int CreateCalls, RefundCalls, ExpireCalls;
    public async Task<StripeSession> CreateAsync(Payment payment, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CreateCalls);
        var id = "cs_test_" + payment.Id.ToString("N");
        var session = Sessions.GetOrAdd(id, _ => new(id, $"https://checkout.stripe.com/c/pay/{id}", false, "payment", "open", "unpaid", payment.AmountMinor, payment.Currency, payment.ReservationId.ToString(), payment.Id.ToString(), null, null, 0, null, false, payment.CheckoutExpiresAt, payment.ReservationId.ToString()));
        if (CreateStarted is not null) { CreateStarted.TrySetResult(); await ResumeCreate!.Task.WaitAsync(cancellationToken); }
        if (LoseCreateResponse) { LoseCreateResponse = false; throw new HttpRequestException("Lost test response"); }
        return session;
    }
    public async Task<StripeSession> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (FailReads) throw new HttpRequestException("Test provider unavailable");
        if (ReadStarted is not null) { ReadStarted.TrySetResult(); await ResumeRead!.Task.WaitAsync(cancellationToken); }
        return Sessions[sessionId];
    }
    public Task ExpireAsync(string sessionId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ExpireCalls);
        Sessions.AddOrUpdate(sessionId, _ => throw new InvalidOperationException(), (_, value) => value.Status == "open" ? value with { Status = "expired", Url = null } : value);
        return Task.CompletedTask;
    }
    public StripeSession Paid(string sessionId)
    {
        var current = Sessions[sessionId];
        var paid = current with { Status = "complete", PaymentStatus = "paid", Url = null, IntentId = "pi_" + current.PaymentId.Replace("-", ""), IntentStatus = "succeeded", IntentReceived = current.Amount, IntentCurrency = current.Currency };
        Sessions[sessionId] = paid; return paid;
    }
    public Task<StripeRefund> RefundAsync(Payment payment, RefundJob job, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref RefundCalls);
        if (FailRefunds) throw new HttpRequestException("Test refund unavailable");
        var id = "re_" + job.Id.ToString("N");
        var result = Refunds.GetOrAdd(id, _ => new(id, RefundStatus, payment.PaymentIntentId!, job.AmountMinor, payment.Currency, false));
        if (LoseRefundResponse) { LoseRefundResponse = false; throw new HttpRequestException("Lost test refund response"); }
        return Task.FromResult(result);
    }
    public Task<StripeRefund> GetRefundAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Refunds[id]);
}
