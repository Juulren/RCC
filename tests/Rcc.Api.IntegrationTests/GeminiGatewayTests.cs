using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rcc.Api.Recommendations;
namespace Rcc.Api.IntegrationTests;

public sealed class GeminiGatewayTests
{
    private static readonly Candidate Candidate = new("option-0", [Guid.NewGuid()], 200, 1, 80, ["PRIVATE NAME"]);
    private static AiSettings Settings() => new() { Enabled = true, Provider = "Gemini", Model = GeminiGateway.AllowedModel, ApiKey = "fixture-not-a-real-credential" };
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public string? Body, Key; public Uri? Url; public int Calls;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Ranking = "{\"options\":[{\"id\":\"option-0\"}]}";
        public string Finish = "STOP";
        public string? Raw;
        public HttpClient CreateClient(string name) { Assert.Equal("gemini", name); return new(this, false); }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(token); Url = request.RequestUri;
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            return new(Status) { Content = new StringContent(Raw ?? JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = Finish, content = new { parts = new[] { new { text = Ranking } } } } } })) };
        }
    }
    [Theory]
    [InlineData("es", "habitación(es)")]
    [InlineData("en", "room(s)")]
    public async Task Request_uses_fixed_model_header_only_private_facts_and_schema(string locale, string expected)
    {
        using var transport = new Transport(); var settings = Settings();
        var result = await new GeminiGateway(transport, Options.Create(settings)).RankAsync([Candidate], ["river"], locale, default);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", transport.Url!.AbsoluteUri);
        Assert.Equal(settings.ApiKey, transport.Key); Assert.DoesNotContain(settings.ApiKey, transport.Body!);
        Assert.DoesNotContain("PRIVATE NAME", transport.Body!); Assert.DoesNotContain(Candidate.RoomTypeIds[0].ToString(), transport.Body!);
        using var body = JsonDocument.Parse(transport.Body!);
        var config = body.RootElement.GetProperty("generationConfig");
        Assert.False(config.TryGetProperty("thinkingConfig", out _));
        Assert.False(config.TryGetProperty("temperature", out _));
        Assert.Equal(1024, config.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.Equal("option-0", config.GetProperty("responseJsonSchema").GetProperty("properties").GetProperty("options").GetProperty("items").GetProperty("properties").GetProperty("id").GetProperty("enum")[0].GetString());
        Assert.Contains(expected, result.Single().Explanation); Assert.Contains("CNY 200.00", result.Single().Explanation);
        Assert.True(RecommendationService.ValidRanking(result, [Candidate]));
    }
    [Theory]
    [InlineData(429)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task Quota_or_provider_failure_is_one_attempt_without_retry(int status)
    {
        using var transport = new Transport { Status = (HttpStatusCode)status };
        await Assert.ThrowsAsync<HttpRequestException>(() => new GeminiGateway(transport, Options.Create(Settings())).RankAsync([Candidate], [], "es", default));
        Assert.Equal(1, transport.Calls);
    }
    [Theory]
    [InlineData("MAX_TOKENS", "{\"options\":[{\"id\":\"option-0\"}]}")]
    [InlineData("SAFETY", "{\"options\":[{\"id\":\"option-0\"}]}")]
    [InlineData("STOP", "invalid")]
    [InlineData("STOP", "{\"options\":[]}")]
    [InlineData("STOP", "{\"options\":[{\"id\":\"invented\"}]}")]
    [InlineData("STOP", "{\"options\":[{\"id\":\"option-0\"},{\"id\":\"option-0\"}]}")]
    public async Task Incomplete_or_untrusted_ranking_is_rejected(string finish, string ranking)
    {
        using var transport = new Transport { Finish = finish, Ranking = ranking };
        await Assert.ThrowsAnyAsync<JsonException>(() => new GeminiGateway(transport, Options.Create(Settings())).RankAsync([Candidate], [], "es", default));
    }
    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        using var transport = new Transport { Raw = new string(' ', 65537) };
        await Assert.ThrowsAnyAsync<JsonException>(() => new GeminiGateway(transport, Options.Create(Settings())).RankAsync([Candidate], [], "es", default));
    }
    [Theory]
    [InlineData("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}")]
    [InlineData("{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":null}]}}]}")]
    [InlineData("{\"candidates\":[]}")]
    public async Task Blocked_or_empty_provider_payload_is_rejected(string raw)
    {
        using var transport = new Transport { Raw = raw };
        await Assert.ThrowsAnyAsync<JsonException>(() => new GeminiGateway(transport, Options.Create(Settings())).RankAsync([Candidate], [], "es", default));
    }
    [Theory]
    [InlineData(false, GeminiGateway.AllowedModel)]
    [InlineData(true, "paid-or-unknown-model")]
    [InlineData(true, "gemini-2.5-flash-lite")]
    public async Task Disabled_or_non_allowlisted_model_never_sends_a_request(bool enabled, string model)
    {
        using var transport = new Transport(); var settings = Settings(); settings.Enabled = enabled; settings.Model = model;
        Assert.False(settings.Ready);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GeminiGateway(transport, Options.Create(settings)).RankAsync([Candidate], [], "es", default));
        Assert.Equal(0, transport.Calls);
    }
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Gemini_configuration_wins_over_legacy_provider_without_fallback(string enabled, bool ready)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:Enabled"] = enabled,
            ["Gemini:ApiKey"] = "fixture-not-a-real-credential",
            ["OpenAI:Enabled"] = "true",
            ["OpenAI:Provider"] = "Ollama",
            ["OpenAI:Model"] = "qwen2.5:1.5b"
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddRccAi(config);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<AiSettings>>().Value;
        Assert.Equal("Gemini", settings.Provider); Assert.Equal(GeminiGateway.AllowedModel, settings.Model); Assert.Equal(ready, settings.Ready);
        Assert.IsType<GeminiGateway>(scope.ServiceProvider.GetRequiredService<IRecommendationGateway>());
    }
}
