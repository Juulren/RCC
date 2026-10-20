using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Stripe;
using Stripe.Checkout;

namespace Rcc.Api.Payments;

public sealed class StripeSettings
{
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string PublicOrigin { get; set; } = "";
    public bool Ready => SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) && WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal) && ValidOrigin(PublicOrigin);
    public static bool ValidOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));
}

public sealed record StripeSession(string Id, string? Url, bool Live, string Mode, string Status, string PaymentStatus, long Amount, string Currency, string ReservationId, string PaymentId, string? IntentId, string? IntentStatus, long IntentReceived, string? IntentCurrency, bool IntentLive, DateTimeOffset ExpiresAt, string? ClientReferenceId = null);
public sealed record StripeRefund(string Id, string Status, string IntentId, long Amount, string Currency, bool Live);
public interface IStripeGateway
{
    Task<StripeSession> CreateAsync(Payment payment, CancellationToken cancellationToken);
    Task<StripeSession> GetAsync(string sessionId, CancellationToken cancellationToken);
    Task ExpireAsync(string sessionId, CancellationToken cancellationToken);
    Task<StripeRefund> RefundAsync(Payment payment, RefundJob job, CancellationToken cancellationToken);
    Task<StripeRefund> GetRefundAsync(string id, CancellationToken cancellationToken);
}

public sealed class StripeGateway(IOptions<StripeSettings> settings, IHttpClientFactory http) : IStripeGateway
{
    private StripeClient Client() => new(settings.Value.SecretKey, httpClient: new SystemNetHttpClient(http.CreateClient("stripe"), maxNetworkRetries: 0));
    private static RequestOptions Request(string key) => new() { IdempotencyKey = key };
    public async Task<StripeSession> CreateAsync(Payment payment, CancellationToken cancellationToken)
    {
        var url = $"{payment.ReturnOrigin.TrimEnd('/')}/mis-reservas?reserva={payment.ReservationId}";
        var metadata = new Dictionary<string, string> { ["rcc_reservation"] = payment.ReservationId.ToString(), ["rcc_payment"] = payment.Id.ToString() };
        var session = await new SessionService(Client()).CreateAsync(new SessionCreateOptions
        {
            Mode = "payment", AllowedPaymentMethodTypes = ["card"], SuccessUrl = url + "&pago=retorno", CancelUrl = url + "&pago=cancelado",
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false }, AutomaticTax = new SessionAutomaticTaxOptions { Enabled = false }, AllowPromotionCodes = false,
            ClientReferenceId = payment.ReservationId.ToString(), Metadata = metadata,
            Locale = payment.Reservation.Locale, ExpiresAt = payment.CheckoutExpiresAt.UtcDateTime,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
            LineItems = [new SessionLineItemOptions { Quantity = 1, PriceData = new SessionLineItemPriceDataOptions { Currency = payment.Currency, UnitAmount = payment.AmountMinor, ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"RCC · Test reservation {payment.ReservationId.ToString()[..8]}" } } }],
            Expand = ["payment_intent"]
        }, Request($"rcc-checkout-{payment.Id:N}"), cancellationToken);
        return View(session);
    }
    public async Task<StripeSession> GetAsync(string sessionId, CancellationToken cancellationToken) => View(await new SessionService(Client()).GetAsync(sessionId, new SessionGetOptions { Expand = ["payment_intent"] }, cancellationToken: cancellationToken));
    public async Task ExpireAsync(string sessionId, CancellationToken cancellationToken) => _ = await new SessionService(Client()).ExpireAsync(sessionId, cancellationToken: cancellationToken);
    public async Task<StripeRefund> RefundAsync(Payment payment, RefundJob job, CancellationToken cancellationToken) => View(await new RefundService(Client()).CreateAsync(new RefundCreateOptions { PaymentIntent = payment.PaymentIntentId, Amount = job.AmountMinor, Metadata = new() { ["rcc_refund"] = job.Id.ToString() } }, Request($"rcc-refund-{job.Id:N}"), cancellationToken));
    public async Task<StripeRefund> GetRefundAsync(string id, CancellationToken cancellationToken) => View(await new RefundService(Client()).GetAsync(id, cancellationToken: cancellationToken));
    private static StripeSession View(Session session)
    {
        if (session.Url is not null && (!Uri.TryCreate(session.Url, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "checkout.stripe.com" || !string.IsNullOrEmpty(url.UserInfo))) throw new InvalidOperationException("STRIPE_URL_INVALID");
        return new(session.Id, session.Url, session.Livemode, session.Mode, session.Status, session.PaymentStatus, session.AmountTotal ?? -1, session.Currency,
            session.Metadata?.GetValueOrDefault("rcc_reservation") ?? "", session.Metadata?.GetValueOrDefault("rcc_payment") ?? "", session.PaymentIntentId, session.PaymentIntent?.Status,
            session.PaymentIntent?.AmountReceived ?? 0, session.PaymentIntent?.Currency, session.PaymentIntent?.Livemode ?? false, new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero), session.ClientReferenceId);
    }
    private static StripeRefund View(Refund refund) => new(refund.Id, refund.Status, refund.PaymentIntentId, refund.Amount, refund.Currency, false);
}
