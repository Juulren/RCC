using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rcc.Api.Recommendations;
namespace Rcc.Api.IntegrationTests;

public sealed class OpenAiGatewayTests
{
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public string? Body, Authorization, Host; public bool Refusal, Incomplete; public HttpStatusCode Status = HttpStatusCode.OK;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Body = await request.Content!.ReadAsStringAsync(token); Authorization = request.Headers.Authorization?.ToString(); Host = request.RequestUri!.Host;
            return new(Status) { Content = new StringContent(JsonSerializer.Serialize(new { status = Incomplete ? "incomplete" : "completed", output = new[] { new { type = "message", content = new[] { new { type = Refusal ? "refusal" : "output_text", text = "{\"options\":[{\"id\":\"option-0\",\"explanation\":\"Valid option\"}]}" } } } } })) };
        }
    }
    [Fact]
    public async Task Responses_transport_is_structured_bounded_private_and_does_not_store_response()
    {
        using var transport = new Transport(); var gateway = new OpenAiGateway(transport, Options.Create(new AiSettings { ApiKey = "sk-fixture", Model = "configured-model", MaxOutputTokens = 900 }));
        var result = await gateway.RankAsync([new("option-0", [Guid.NewGuid()], 200, 1, 100, ["PRIVATE NAME MUST NOT LEAVE RCC"])], ["river"], "es", default);
        Assert.Equal("Valid option", result.Single().Explanation); Assert.Equal("api.openai.com", transport.Host); Assert.Equal("Bearer sk-fixture", transport.Authorization);
        var root = JsonSerializer.Deserialize<JsonElement>(transport.Body!); Assert.False(root.GetProperty("store").GetBoolean()); Assert.Equal(900, root.GetProperty("max_output_tokens").GetInt32()); Assert.Equal("configured-model", root.GetProperty("model").GetString()); Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean()); Assert.DoesNotContain("PRIVATE NAME", transport.Body); Assert.DoesNotContain("roomTypeIds", transport.Body); Assert.DoesNotContain("childAges", transport.Body); Assert.DoesNotContain("email", root.GetProperty("input").GetString());
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Refusals_and_incomplete_outputs_are_not_treated_as_recommendations(bool refusal, bool incomplete)
    {
        using var transport = new Transport { Refusal = refusal, Incomplete = incomplete }; var gateway = new OpenAiGateway(transport, Options.Create(new AiSettings()));
        await Assert.ThrowsAsync<JsonException>(() => gateway.RankAsync([new("option-0", [], 200, 0, 50, [])], [], "en", default));
    }
    [Fact]
    public async Task Provider_rate_limit_is_not_a_successful_ai_response()
    {
        using var transport = new Transport { Status = HttpStatusCode.TooManyRequests }; var gateway = new OpenAiGateway(transport, Options.Create(new AiSettings()));
        await Assert.ThrowsAsync<HttpRequestException>(() => gateway.RankAsync([], [], "en", default));
    }
}
