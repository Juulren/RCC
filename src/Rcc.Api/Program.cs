using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Rcc.Api.Data;
using Rcc.Api.Health;
using Rcc.Api.Auth;
using Rcc.Api.Catalog;
using Rcc.Api.Reservations;

var initializeIdentity = args.Contains("--initialize-identity");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--initialize-identity").ToArray());
var connectionString = builder.Configuration.GetConnectionString("Rcc");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:Rcc mediante user-secrets o ConnectionStrings__Rcc.");
}

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<RccDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("postgresql", tags: ["ready"]);
builder.Services.AddRccIdentity(builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ReservationExpiry>();

var app = builder.Build();
if (initializeIdentity)
{
    await IdentityBootstrap.InitializeAsync(app.Services, app.Configuration);
    return;
}
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
app.MapGet("/api/status", () => Results.Ok(new
{
    application = "RCC",
    module = 5,
    stage = "reservations"
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
