using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rcc.Api.Services;
namespace Rcc.Api.IntegrationTests;

public sealed class ExternalServicesTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Transport(Clock clock) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls; public bool Fail; public string? Content, Key, Auth; public HttpStatusCode Status = HttpStatusCode.OK;
        public string Response = "";
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            if (Fail) throw new HttpRequestException();
            if (request.Content is not null) Content = await request.Content.ReadAsStringAsync(token);
            Key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            Auth = request.Headers.Authorization?.ToString();
            return new(Status) { Content = new StringContent(Response.Length > 0 ? Response : JsonSerializer.Serialize(new { current = new { temperature_2m = 23.4, apparent_temperature = 25.2, weather_code = 3, wind_speed_10m = 7.1, time = clock.Now.UtcDateTime.AddHours(8).ToString("yyyy-MM-ddTHH:mm") } })) };
        }
    }
    [Fact]
    public async Task Weather_coalesces_parallel_requests_and_caches_fifteen_minutes()
    {
        var clock = new Clock(); using var transport = new Transport(clock); var weather = new WeatherService(transport, clock);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => weather.GetAsync(default)));
        Assert.Equal(1, transport.Calls); Assert.All(results, x => { Assert.True(x.Available); Assert.False(x.Stale); Assert.Equal(23.4, x.Current!.Temperature); });
        clock.Now = clock.Now.AddMinutes(14); await weather.GetAsync(default); Assert.Equal(1, transport.Calls);
        clock.Now = clock.Now.AddMinutes(2); await weather.GetAsync(default); Assert.Equal(2, transport.Calls);
    }
    [Fact]
    public async Task Weather_failure_preserves_labelled_recent_data_then_discards_old_data()
    {
        var clock = new Clock(); using var transport = new Transport(clock); var weather = new WeatherService(transport, clock);
        await weather.GetAsync(default); clock.Now = clock.Now.AddMinutes(16); transport.Fail = true;
        var stale = await weather.GetAsync(default); Assert.True(stale.Stale); Assert.True(stale.Available);
        await weather.GetAsync(default); Assert.Equal(2, transport.Calls);
        clock.Now = clock.Now.AddHours(2); var unavailable = await weather.GetAsync(default); Assert.False(unavailable.Available); Assert.Null(unavailable.Current);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"current\":{\"temperature_2m\":100}}")]
    public async Task Invalid_weather_does_not_create_fake_conditions(string payload)
    {
        var clock = new Clock(); using var transport = new Transport(clock) { Response = payload };
        Assert.False((await new WeatherService(transport, clock).GetAsync(default)).Available);
    }
    [Fact]
    public async Task Resend_transport_uses_bearer_stable_key_and_snapshot_plain_text()
    {
        var clock = new Clock(); var id = Guid.NewGuid().ToString(); using var transport = new Transport(clock) { Response = JsonSerializer.Serialize(new { id }) };
        var gateway = new ResendGateway(transport, Options.Create(new EmailSettings { ApiKey = "re_test" }));
        var message = new Notification { Id = Guid.NewGuid(), Sender = "RCC <test@example.test>", Recipient = "owner@example.test", Subject = "Cancelada", Body = "Texto <original>" };
        var outcome = await gateway.SendAsync(message, default);
        Assert.Equal("Accepted", outcome.State); Assert.Equal(id, outcome.ProviderId); Assert.Equal("Bearer re_test", transport.Auth); Assert.Equal($"rcc-notification-{message.Id:N}", transport.Key);
        var body = JsonSerializer.Deserialize<JsonElement>(transport.Content!); Assert.Equal(message.Body, body.GetProperty("text").GetString()); Assert.Equal(message.Sender, body.GetProperty("from").GetString()); Assert.False(body.TryGetProperty("html", out _));
    }
    [Theory]
    [InlineData(429, "Pending")]
    [InlineData(503, "Pending")]
    [InlineData(401, "Review")]
    [InlineData(422, "Review")]
    public async Task Resend_retries_transient_failures_but_stops_invalid_configuration(int status, string state)
    {
        var clock = new Clock(); using var transport = new Transport(clock) { Status = (HttpStatusCode)status };
        var gateway = new ResendGateway(transport, Options.Create(new EmailSettings { ApiKey = "re_fixture" }));
        Assert.Equal(state, (await gateway.SendAsync(new Notification(), default)).State);
    }
}
