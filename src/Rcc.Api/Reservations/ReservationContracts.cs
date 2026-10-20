namespace Rcc.Api.Reservations;

public sealed record GuestInput(int Adults, int[]? ChildAges, Guid? RoomTypeId = null);
public sealed record StayInput(DateOnly Arrival, DateOnly Departure, GuestInput?[]? Rooms);
public sealed record ReserveInput(DateOnly Arrival, DateOnly Departure, GuestInput?[]? Rooms, string? PriceFingerprint, string? Locale);
public sealed record QuoteLine(Guid RoomTypeId, string NameEs, string NameEn, int Adults, int[] ChildAges, decimal NightlyRate, decimal Subtotal);
public sealed record QuoteView(DateOnly Arrival, DateOnly Departure, int Nights, List<QuoteLine> Items, decimal Total, string PriceFingerprint, string Currency = "CNY", bool IsDemo = true);
