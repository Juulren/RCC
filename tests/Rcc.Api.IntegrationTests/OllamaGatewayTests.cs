using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rcc.Api.Recommendations;
namespace Rcc.Api.IntegrationTests;

public sealed class OllamaGatewayTests
{
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public string? Body; public Uri? Url; public string? Authorization;
        public bool Incomplete; public bool Malformed; public HttpStatusCode Status = HttpStatusCode.OK;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Body = await request.Content!.ReadAsStringAsync(token); Url = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            return new(Status) { Content = new StringContent(JsonSerializer.Serialize(new { done = true, done_reason = Incomplete ? "length" : "stop", response = Malformed ? "invalid" : "{\"options\":[{\"id\":\"option-0\",\"explanation\":\"Valid local option\"}]}" })) };
        }
    }
    [Fact]
    public async Task Local_transport_is_loopback_structured_private_and_keyless()
    {
        using var transport = new Transport();
        var settings = new AiSettings { Enabled = true, Provider = "Ollama", Model = "qwen2.5:1.5b" };
        Assert.True(settings.Ready);
        var gateway = new OllamaGateway(transport, Options.Create(settings));
        var result = await gateway.RankAsync([new("option-0", [Guid.NewGuid()], 200, 1, 80, ["PRIVATE NAME"])], ["river"], "es", default);
        Assert.Contains("CNY 200.00", result.Single().Explanation);
        Assert.Contains("80 m²", result.Single().Explanation);
        Assert.DoesNotContain("Valid local option", result.Single().Explanation);
        Assert.Equal("http://127.0.0.1:11434/api/generate", transport.Url!.AbsoluteUri); Assert.Null(transport.Authorization);
        var body = JsonSerializer.Deserialize<JsonElement>(transport.Body!);
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.Equal("0s", body.GetProperty("keep_alive").GetString());
        Assert.Equal(4096, body.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal("option-0", body.GetProperty("format").GetProperty("properties").GetProperty("options").GetProperty("items").GetProperty("properties").GetProperty("id").GetProperty("enum")[0].GetString());
        Assert.DoesNotContain("PRIVATE NAME", transport.Body); Assert.DoesNotContain("roomTypeIds", transport.Body);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Truncated_or_invalid_json_is_rejected(bool incomplete, bool malformed)
    {
        using var transport = new Transport { Incomplete = incomplete, Malformed = malformed };
        await Assert.ThrowsAnyAsync<JsonException>(() => new OllamaGateway(transport, Options.Create(new AiSettings())).RankAsync([], [], "es", default));
    }
    [Fact]
    public async Task Missing_model_or_server_failure_is_not_success()
    {
        using var transport = new Transport { Status = HttpStatusCode.NotFound };
        await Assert.ThrowsAsync<HttpRequestException>(() => new OllamaGateway(transport, Options.Create(new AiSettings())).RankAsync([], [], "es", default));
    }
    [Fact]
    public void Unknown_provider_never_enables_ai()
    {
        Assert.False(new AiSettings { Enabled = true, Provider = "other", Model = "model", ApiKey = "sk-fixture" }.Ready);
        Assert.False(new AiSettings { Enabled = true, Provider = "Ollama" }.Ready);
        Assert.False(new AiSettings { Enabled = true, Provider = "Ollama", Model = "paid-model:cloud" }.Ready);
        Assert.False(new AiSettings { Enabled = true, Provider = "OpenAI", Model = "model" }.Ready);
    }
}
