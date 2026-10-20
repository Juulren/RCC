using System.Text.Json;
namespace Rcc.Api.Services;

public sealed record WeatherSnapshot(double Temperature, double ApparentTemperature, int Code, double WindSpeed, string ObservedAt, DateTimeOffset FetchedAt);
public sealed record WeatherResult(bool Available, bool Stale, WeatherSnapshot? Current, string TimeZone = "Asia/Shanghai", string Source = "Open-Meteo");
public sealed class WeatherService(IHttpClientFactory clients, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1);
    private WeatherSnapshot? cached;
    private DateTimeOffset nextAttempt;
    public async Task<WeatherResult> GetAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var now = clock.GetUtcNow();
            if (now >= nextAttempt)
            {
                nextAttempt = now.AddMinutes(1);
                try
                {
                    using var response = await clients.CreateClient("weather").GetAsync("https://api.open-meteo.com/v1/forecast?latitude=29.57&longitude=106.57&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m&timezone=Asia%2FShanghai&forecast_days=1", token);
                    response.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    var current = json.RootElement.GetProperty("current");
                    var snapshot = new WeatherSnapshot(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(), current.GetProperty("weather_code").GetInt32(), current.GetProperty("wind_speed_10m").GetDouble(), current.GetProperty("time").GetString()!, now);
                    if (!double.IsFinite(snapshot.Temperature) || snapshot.Temperature is < -60 or > 65 || !double.IsFinite(snapshot.ApparentTemperature) || !double.IsFinite(snapshot.WindSpeed) || snapshot.WindSpeed < 0 || !DateTime.TryParse(snapshot.ObservedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var observed) || Math.Abs((now.UtcDateTime.AddHours(8) - observed).TotalHours) > 3) throw new JsonException();
                    cached = snapshot; nextAttempt = now.AddMinutes(15);
                }
                catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException || error is OperationCanceledException && !token.IsCancellationRequested) { /* Keep last known data, never invent weather. */ }
            }
            var usable = cached is not null && now - cached.FetchedAt <= TimeSpan.FromHours(2);
            return new(usable, usable && now - cached!.FetchedAt >= TimeSpan.FromMinutes(15), usable ? cached : null);
        }
        finally { gate.Release(); }
    }
}
