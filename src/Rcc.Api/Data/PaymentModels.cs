namespace Rcc.Api.Data;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public string State { get; set; } = "Creating";
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "cny";
    public string ReturnOrigin { get; set; } = "";
    public string? SessionId { get; set; }
    public string? PaymentIntentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset CheckoutExpiresAt { get; set; }
    public DateTimeOffset NextSyncAt { get; set; }
    public string? LastError { get; set; }
    public int SyncAttempts { get; set; }
    public RefundJob? Refund { get; set; }
}

public sealed class PaymentEvent
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public Guid? PaymentId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string Outcome { get; set; } = "";
}

public sealed class RefundJob
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public string State { get; set; } = "Pending";
    public string? StripeRefundId { get; set; }
    public long AmountMinor { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? LastError { get; set; }
}
