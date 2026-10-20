namespace Rcc.Api.Data;

public sealed class Reservation
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public DateOnly Arrival { get; set; }
    public DateOnly Departure { get; set; }
    public string Status { get; set; } = "PendingPayment";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Currency { get; set; } = "CNY";
    public decimal Total { get; set; }
    public string Locale { get; set; } = "es";
    public List<ReservationItem> Items { get; set; } = [];
}

public sealed class ReservationItem
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public Guid RoomId { get; set; }
    public string NameEs { get; set; } = "";
    public string NameEn { get; set; } = "";
    public decimal NightlyRate { get; set; }
    public int Adults { get; set; }
    public int[] ChildAges { get; set; } = [];
    public decimal Subtotal { get; set; }
}

public sealed class IdempotencyRequest
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid ResourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
