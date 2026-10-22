using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Rcc.Api.Recommendations;
namespace Rcc.AiRelay;

public static class RelayApp
{
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 16384);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.Configure<AiSettings>(settings => { settings.Enabled = true; settings.Provider = "Ollama"; settings.Model = "qwen2.5:1.5b"; });
        builder.Services.AddHttpClient("ollama", client => { client.Timeout = TimeSpan.FromSeconds(110); client.MaxResponseContentBufferSize = 65536; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        builder.Services.AddScoped<IRecommendationGateway, OllamaGateway>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetFixedWindowLimiter("all", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddConcurrencyLimiter("model", options => { options.PermitLimit = 1; options.QueueLimit = 0; });
        });
    }
    public static void Map(WebApplication app)
    {
        var token = app.Configuration["Relay:Token"] ?? "";
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new InvalidOperationException("Configure una credencial aleatoria de 32 bytes para el puente.");
        if (!DateTimeOffset.TryParse(app.Configuration["Relay:ExpiresUtc"], out var expiry) || expiry <= DateTimeOffset.UtcNow || expiry > DateTimeOffset.UtcNow.AddHours(8)) throw new InvalidOperationException("Configure vencimiento del puente dentro de las proximas 8 horas.");
        var requests = 0;
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + token));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            // Reject browser-origin traffic even if someone puts a credential into frontend code.
            var supplied = context.Request.Headers.Authorization;
            if (DateTimeOffset.UtcNow >= expiry || context.Request.Headers.ContainsKey("Origin") || supplied.Count != 1 || supplied[0]!.Length > 100 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied[0]!)), expected))
            { context.Response.StatusCode = 401; return; }
            if (context.Request.ContentLength > 16384) { context.Response.StatusCode = 413; return; }
            await next(context);
        });
        app.UseRateLimiter();
        app.MapGet("/health", () => Results.Ok(new { ready = true }));
        app.MapPost("/v1/rank", async (RelayInput input, IRecommendationGateway gateway, CancellationToken token) =>
        {
            if (!input.IsValid()) return Results.BadRequest(new { code = "INVALID_RANK_INPUT" });
            if (Interlocked.Increment(ref requests) > 30) return Results.StatusCode(429);
            var candidates = input.ToCandidates();
            try
            {
                var ranked = await gateway.RankAsync(candidates, input.Preferences, input.Locale, token);
                if (ranked is not { Count: >= 1 and <= 3 } || ranked.Select(c => c.Id).Distinct().Count() != ranked.Count || ranked.Any(c => !candidates.Any(x => x.Id == c.Id))) return Results.StatusCode(502);
                return Results.Ok(ranked);
            }
            catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException || error is OperationCanceledException && !token.IsCancellationRequested)
            { return Results.StatusCode(503); }
        }).RequireRateLimiting("model");
    }
}
