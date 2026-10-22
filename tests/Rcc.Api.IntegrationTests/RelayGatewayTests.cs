using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rcc.Api.Recommendations;
namespace Rcc.Api.IntegrationTests;

public sealed class RelayGatewayTests
{
    private sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls; public string? Body, Auth; public Uri? Url; public HttpStatusCode Status = HttpStatusCode.OK;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Url = request.RequestUri; Auth = request.Headers.Authorization?.ToString(); Body = await request.Content!.ReadAsStringAsync(token);
            return new(Status) { Content = JsonContent.Create(new[] { new RankedChoice("option-0", "Checked facts") }) };
        }
    }
    private static AiSettings Settings() => new() { Enabled = true, Provider = "OllamaRelay", Model = "qwen2.5:1.5b", RelayUrl = "https://rcc-demo.trycloudflare.com/", RelayToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
    [Fact]
    public async Task Only_numeric_candidates_leave_backend_and_token_is_only_in_header()
    {
        using var transport = new Transport(); var settings = Settings();
        var result = await new RelayGateway(transport, Options.Create(settings)).RankAsync([new("option-0", [Guid.NewGuid()], 1760, 1, 60, ["PRIVATE NAME"])], ["river"], "es", default);
        Assert.Equal("option-0", result.Single().Id); Assert.Equal("https://rcc-demo.trycloudflare.com/v1/rank", transport.Url!.AbsoluteUri);
        Assert.Equal("Bearer " + settings.RelayToken, transport.Auth);
        Assert.DoesNotContain(settings.RelayToken, transport.Body); Assert.DoesNotContain("PRIVATE NAME", transport.Body); Assert.DoesNotContain("roomTypeIds", transport.Body, StringComparison.OrdinalIgnoreCase);
        var body = JsonSerializer.Deserialize<JsonElement>(transport.Body!);
        Assert.Equal(1760, body.GetProperty("candidates")[0].GetProperty("totalCny").GetDecimal());
        Assert.Equal(1, body.GetProperty("candidates")[0].GetProperty("rooms").GetInt32());
    }
    [Fact]
    public async Task Invalid_destination_is_rejected_before_sending_credential()
    {
        using var transport = new Transport(); var settings = Settings(); settings.RelayUrl = "https://attacker.test/";
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RelayGateway(transport, Options.Create(settings)).RankAsync([], [], "es", default));
        Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task Redirects_are_not_successful_rankings()
    {
        using var transport = new Transport { Status = HttpStatusCode.Redirect };
        await Assert.ThrowsAsync<HttpRequestException>(() => new RelayGateway(transport, Options.Create(Settings())).RankAsync([], [], "es", default));
    }
}
