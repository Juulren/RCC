using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;
using Rcc.Api.Health;
using Rcc.Api.Auth;
using Rcc.Api.Catalog;
using Rcc.Api.Reservations;
using Rcc.Api.Payments;
using Rcc.Api.Services;
using Rcc.Api.Recommendations;
using Rcc.Api.Deployment;

if (args.Contains("--prepare-deployment")) { DeploymentSecrets.Prepare(); return; }
var migrateOnly = args.Contains("--migrate");
var initializeIdentity = args.Contains("--initialize-identity");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not ("--initialize-identity" or "--migrate")).ToArray());
var connectionString = builder.Configuration.GetConnectionString("Rcc");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:Rcc mediante user-secrets o ConnectionStrings__Rcc.");
}

if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) && port is > 0 and <= 65535)
    builder.WebHost.UseUrls("http://0.0.0.0:" + port);
builder.AddRccProduction();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<RccDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("postgresql", tags: ["ready"]);
builder.Services.AddRccIdentity(builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationExpiry>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddHttpClient("stripe", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<PaymentProcessor>();
builder.Services.AddHostedService<PaymentWorker>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient("weather", client => client.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddHttpClient("email", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<WeatherService>();
builder.Services.AddScoped<IEmailGateway, ResendGateway>();
builder.Services.AddScoped<NotificationProcessor>();
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddHttpClient("openai", client => { client.Timeout = TimeSpan.FromSeconds(10); client.MaxResponseContentBufferSize = 65536; });
builder.Services.AddHttpClient("ollama", client => { client.Timeout = TimeSpan.FromSeconds(120); client.MaxResponseContentBufferSize = 65536; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
builder.Services.AddScoped<OpenAiGateway>();
builder.Services.AddScoped<OllamaGateway>();
builder.Services.AddHttpClient("ai-relay", client => { client.Timeout = TimeSpan.FromSeconds(120); client.MaxResponseContentBufferSize = 65536; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
builder.Services.AddScoped<RelayGateway>();
builder.Services.AddScoped<IRecommendationGateway>(services =>
    services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value.Provider == "Ollama"
        ? services.GetRequiredService<OllamaGateway>()
        : services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value.Provider == "OllamaRelay"
            ? services.GetRequiredService<RelayGateway>() : services.GetRequiredService<OpenAiGateway>());
builder.Services.AddScoped<RecommendationService>();
var app = builder.Build();
if (migrateOnly || builder.Configuration.GetValue<bool>("Deployment:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RccDbContext>().Database.MigrateAsync();
    if (migrateOnly) return;
}
if (initializeIdentity)
{
    await IdentityBootstrap.InitializeAsync(app.Services, app.Configuration);
    return;
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRccAuth();
app.MapRccCatalog();
app.MapRccReservations();
app.MapRccPayments();
app.MapRccServices();
app.MapRccRecommendations();
app.MapGet("/api/status", () => Results.Ok(new
{
    application = "RCC",
    module = 9,
    stage = "deployment-preparation"
}));
app.MapHealthChecks("/api/health/live", HealthOptions(ready: false));
app.MapHealthChecks("/api/health/ready", HealthOptions(ready: true));

// Unknown API routes must not return the SPA or expose infrastructure details.
app.Map("/api/{**path}", () => Results.Problem(statusCode: 404, title: "API route not found."));
app.MapFallbackToFile("index.html");
app.Run();

static HealthCheckOptions HealthOptions(bool ready) => new()
{
    Predicate = registration => ready && registration.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }
};

public partial class Program;
