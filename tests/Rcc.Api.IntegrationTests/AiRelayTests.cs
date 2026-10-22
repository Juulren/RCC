using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rcc.AiRelay;
using Rcc.Api.Recommendations;
namespace Rcc.Api.IntegrationTests;

public sealed class AiRelayTests
{
    private sealed class Fake : IRecommendationGateway
    {
        public int Calls; public TaskCompletionSource? Release;
        public async Task<List<RankedChoice>> RankAsync(List<Candidate> candidates, string[] preferences, string locale, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            if (Release is not null) await Release.Task.WaitAsync(token);
            return [new(candidates[0].Id, "Server checked facts")];
        }
    }
    private sealed class Host(WebApplication app, HttpClient client, string token, Fake fake) : IAsyncDisposable
    {
        public HttpClient Client => client; public Fake Gateway => fake;
        public void Authenticate() => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    }
    private static async Task<Host> Start()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Relay:Token"] = secret, ["Relay:ExpiresUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O") });
        RelayApp.Configure(builder);
        var fake = new Fake(); builder.Services.RemoveAll<IRecommendationGateway>(); builder.Services.AddSingleton<IRecommendationGateway>(fake);
        var app = builder.Build(); RelayApp.Map(app); await app.StartAsync();
        return new Host(app, app.GetTestClient(), secret, fake);
    }
    private static RelayInput Input() => new("es", ["river"], [new("option-0", 1760, 1, 1, 60)]);
    [Fact]
    public async Task Missing_or_wrong_credential_cannot_reach_model_or_health()
    {
        await using var host = await Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/health")).StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "incorrect");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        Assert.Equal(0, host.Gateway.Calls);
    }
    [Fact]
    public async Task Browser_origin_and_arbitrary_ollama_routes_are_rejected()
    {
        await using var host = await Start(); host.Authenticate();
        host.Client.DefaultRequestHeaders.Add("Origin", "https://example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Origin");
        foreach (var route in new[] { "/api/generate", "/api/pull", "/api/tags", "/api/delete", "/api/chat" })
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PostAsJsonAsync(route, Input())).StatusCode);
        Assert.Equal(0, host.Gateway.Calls);
    }
    [Fact]
    public async Task Only_bounded_numeric_candidate_data_is_accepted()
    {
        await using var host = await Start(); host.Authenticate();
        foreach (var invalid in new[] { Input() with { Locale = "xx" }, Input() with { Preferences = ["arbitrary prompt"] }, Input() with { Candidates = [new("unknown", 1, 1, 0, 50)] }, Input() with { Candidates = [new("option-0", -1, 1, 0, 50)] }, Input() with { Candidates = [new("option-0", 1, 9, 0, 50)] }, Input() with { Candidates = null! } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/v1/rank", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/v1/rank", new { locale = "es", preferences = new[] { "river" }, candidates = Input().Candidates, model = "other", prompt = "arbitrary" })).StatusCode);
        Assert.Equal(0, host.Gateway.Calls);
        var response = await host.Client.PostAsJsonAsync("/v1/rank", Input());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("option-0", (await response.Content.ReadFromJsonAsync<List<RankedChoice>>())!.Single().Id);
        Assert.Equal(1, host.Gateway.Calls);
    }
    [Fact]
    public async Task Model_allows_only_one_request_at_a_time_without_queue()
    {
        await using var host = await Start(); host.Authenticate(); host.Gateway.Release = new();
        var first = host.Client.PostAsJsonAsync("/v1/rank", Input());
        for (var i = 0; i < 100 && host.Gateway.Calls == 0; i++) await Task.Delay(10);
        Assert.Equal(1, host.Gateway.Calls);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        host.Gateway.Release.SetResult(); Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
    }
    [Fact]
    public async Task Oversized_payload_is_rejected_before_model()
    {
        await using var host = await Start(); host.Authenticate();
        var response = await host.Client.PostAsync("/v1/rank", new StringContent(new string('x', 17000), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, host.Gateway.Calls);
    }
    [Fact]
    public async Task Lifetime_budget_stops_after_thirty_attempts()
    {
        await using var host = await Start(); host.Authenticate();
        for (var i = 0; i < 30; i++) Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.PostAsJsonAsync("/v1/rank", Input())).StatusCode);
        Assert.Equal(30, host.Gateway.Calls);
    }
    [Fact]
    public async Task Expired_credentials_prevent_startup()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Relay:Token"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), ["Relay:ExpiresUtc"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O") });
        RelayApp.Configure(builder);
        await using var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => RelayApp.Map(app));
    }
    [Theory]
    [InlineData("http://test.trycloudflare.com/")]
    [InlineData("https://attacker.test/")]
    [InlineData("https://test.trycloudflare.com.attacker.test/")]
    [InlineData("https://test.trycloudflare.com/api")]
    [InlineData("https://test.trycloudflare.com/?key=value")]
    [InlineData("https://user@test.trycloudflare.com/")]
    [InlineData("https://test.trycloudflare.com:444/")]
    public void Relay_credentials_are_not_sent_to_unapproved_destinations(string url) => Assert.False(RelayAddress.IsValid(url));
    [Fact]
    public void Relay_supports_only_explicit_https_tunnel_and_full_strength_key()
    {
        Assert.True(RelayAddress.IsValid("https://rcc-demo.trycloudflare.com/"));
        var settings = new AiSettings { Enabled = true, Provider = "OllamaRelay", Model = "qwen2.5:1.5b", RelayUrl = "https://rcc-demo.trycloudflare.com/", RelayToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
        Assert.True(settings.Ready); settings.RelayToken = "weak"; Assert.False(settings.Ready);
    }
}
