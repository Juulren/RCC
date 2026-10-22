using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Rcc.Api.Recommendations;

public sealed class RelayGateway(IHttpClientFactory clients, IOptions<AiSettings> settings) : IRecommendationGateway
{
    public async Task<List<RankedChoice>> RankAsync(List<Candidate> candidates, string[] preferences, string locale, CancellationToken token)
    {
        var config = settings.Value;
        if (!config.Ready || config.Provider != "OllamaRelay") throw new InvalidOperationException("AI_RELAY_NOT_CONFIGURED");
        var input = new RelayInput(locale, preferences, candidates.Select(c => new RelayCandidate(c.Id, c.Total, c.RoomTypeIds.Length, c.RiverRooms, c.Area)).ToArray());
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(config.RelayUrl), "v1/rank"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.RelayToken);
        request.Content = JsonContent.Create(input);
        using var response = await clients.CreateClient("ai-relay").SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.Length > 65536) throw new JsonException("AI_RESPONSE_TOO_LARGE");
        return JsonSerializer.Deserialize<List<RankedChoice>>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new JsonException("AI_INVALID_RESPONSE");
    }
}
