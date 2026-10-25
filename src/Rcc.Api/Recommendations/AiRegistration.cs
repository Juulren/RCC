namespace Rcc.Api.Recommendations;

public static class AiRegistration
{
    public static void AddRccAi(this IServiceCollection services, IConfiguration configuration)
    {
        // Presence of Gemini configuration takes precedence, even when disabled or incomplete.
        // This prevents silently reverting to a legacy provider with a different billing policy.
        var gemini = configuration.GetSection("Gemini").Exists();
        services.Configure<AiSettings>(configuration.GetSection(gemini ? "Gemini" : "OpenAI"));
        if (gemini) services.PostConfigure<AiSettings>(settings =>
        {
            settings.Provider = "Gemini";
            if (string.IsNullOrEmpty(settings.Model)) settings.Model = GeminiGateway.AllowedModel;
        });
        services.AddHttpClient("gemini", client => { client.Timeout = TimeSpan.FromSeconds(15); client.MaxResponseContentBufferSize = 65536; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            .RedactLoggedHeaders(["x-goog-api-key"]);
        services.AddHttpClient("openai", client => { client.Timeout = TimeSpan.FromSeconds(10); client.MaxResponseContentBufferSize = 65536; });
        services.AddHttpClient("ollama", client => { client.Timeout = TimeSpan.FromSeconds(120); client.MaxResponseContentBufferSize = 65536; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        services.AddHttpClient("ai-relay", client => { client.Timeout = TimeSpan.FromSeconds(120); client.MaxResponseContentBufferSize = 65536; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        services.AddScoped<GeminiGateway>();
        services.AddScoped<OpenAiGateway>();
        services.AddScoped<OllamaGateway>();
        services.AddScoped<RelayGateway>();
        services.AddScoped<IRecommendationGateway>(provider =>
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value.Provider switch
            {
                "Gemini" => provider.GetRequiredService<GeminiGateway>(),
                "Ollama" => provider.GetRequiredService<OllamaGateway>(),
                "OllamaRelay" => provider.GetRequiredService<RelayGateway>(),
                _ => provider.GetRequiredService<OpenAiGateway>()
            });
    }
}
