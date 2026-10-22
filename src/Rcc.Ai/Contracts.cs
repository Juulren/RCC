namespace Rcc.Api.Recommendations;
public sealed record Candidate(string Id, Guid[] RoomTypeIds, decimal Total, int RiverRooms, int Area, string[] Names);
public sealed record RankedChoice(string Id, string Explanation);
public sealed class AiSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string RelayUrl { get; set; } = "";
    public string RelayToken { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int MaxOutputTokens { get; set; } = 1024;
    public int DailyRequestLimit { get; set; } = 20;
    public bool Ready => Enabled && (Provider == "OllamaRelay" && Model == "qwen2.5:1.5b" && RelayAddress.IsValid(RelayUrl) && RelayToken.Length == 64 && RelayToken.All(Uri.IsHexDigit) || Provider == "Ollama" && Model == "qwen2.5:1.5b" || Provider == "OpenAI" && ApiKey.StartsWith("sk-", StringComparison.Ordinal)) && !string.IsNullOrWhiteSpace(Model) && Model.Length <= 100 && MaxOutputTokens is >= 256 and <= 2048 && DailyRequestLimit is >= 1 and <= 500;
}
public interface IRecommendationGateway
{
    Task<List<RankedChoice>> RankAsync(List<Candidate> candidates, string[] preferences, string locale, CancellationToken token);
}
