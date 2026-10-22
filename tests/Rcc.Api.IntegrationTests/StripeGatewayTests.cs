using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Payments;

namespace Rcc.Api.IntegrationTests;

public sealed class StripeGatewayTests
{
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public string? Form, Key, Host;
        public bool BadUrl;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Host = request.RequestUri!.Host; Key = request.Headers.GetValues("Idempotency-Key").Single(); Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { id = "cs_test_transport", @object = "checkout.session", url = BadUrl ? "https://example.invalid" : "https://checkout.stripe.com/c/pay/cs_test_transport", livemode = false, mode = "payment", status = "open", payment_status = "unpaid", amount_total = 20000, currency = "cny", metadata = new { }, expires_at = DateTimeOffset.UtcNow.AddMinutes(31).ToUnixTimeSeconds() }), Encoding.UTF8, "application/json") };
        }
    }
    [Fact]
    public async Task Official_sdk_sends_card_only_test_amount_binding_and_stable_key()
    {
        using var transport = new Transport(); var gateway = new StripeGateway(Options.Create(new StripeSettings { SecretKey = "sk_test_transport_fixture" }), transport);
        var payment = new Payment { Id = Guid.NewGuid(), ReservationId = Guid.NewGuid(), Reservation = new Reservation { Locale = "es" }, AmountMinor = 20000, ReturnOrigin = "http://localhost:5080", CheckoutExpiresAt = DateTimeOffset.UtcNow.AddMinutes(31) };
        var result = await gateway.CreateAsync(payment, default);
        var form = transport.Form!.Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => Uri.UnescapeDataString(x[1].Replace('+', ' ')));
        Assert.Equal("api.stripe.com", transport.Host); Assert.Equal($"rcc-checkout-{payment.Id:N}", transport.Key);
        Assert.Equal("card", form["allowed_payment_method_types[0]"]); Assert.Equal("payment", form["mode"]);
        Assert.Equal("20000", form["line_items[0][price_data][unit_amount]"]); Assert.Equal("cny", form["line_items[0][price_data][currency]"]);
        Assert.Equal(payment.Id.ToString(), form["metadata[rcc_payment]"]); Assert.Equal(payment.ReservationId.ToString(), form["client_reference_id"]);
        Assert.StartsWith("http://localhost:5080/mis-reservas?", form["success_url"]); Assert.False(result.Live);
    }
    [Fact]
    public async Task Gateway_rejects_non_stripe_redirect_from_provider_response()
    {
        using var transport = new Transport { BadUrl = true }; var gateway = new StripeGateway(Options.Create(new StripeSettings { SecretKey = "sk_test_transport_fixture" }), transport);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreateAsync(new Payment { Id = Guid.NewGuid(), ReservationId = Guid.NewGuid(), Reservation = new Reservation(), ReturnOrigin = "http://localhost:5080", CheckoutExpiresAt = DateTimeOffset.UtcNow.AddMinutes(31) }, default));
    }
    [Theory]
    [InlineData("sk_live_rejected", "https://rcc.example")]
    [InlineData("sk_test_fixture", "http://rcc.example")]
    [InlineData("sk_test_fixture", "https://rcc.example/path")]
    [InlineData("sk_test_fixture", "https://user@rcc.example")]
    public void Live_keys_and_unsafe_return_origins_disable_payments(string key, string origin) => Assert.False(new StripeSettings { SecretKey = key, WebhookSecret = "whsec_fixture", PublicOrigin = origin }.Ready);
}
