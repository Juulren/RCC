using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Rcc.Api.Data;
using Rcc.Api.Payments;
using Rcc.Api.Reservations;
using Rcc.Testing;

namespace Rcc.PaymentHarness;

public static class HarnessMain
{
    public const string WebhookSecret = "whsec_harness_only_never_a_provider_secret";
    public static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("RCC_PAYMENT_TEST_TOKEN");
        if (token?.Length < 32 || token is null) throw new InvalidOperationException("Set a random RCC_PAYMENT_TEST_TOKEN for this test-only server.");
        var port = args.Length > 0 ? int.Parse(args[0]) : 5084;
        var origin = $"http://127.0.0.1:{port}";
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing"); builder.UseContentRoot(Path.GetFullPath("src/Rcc.Api"));
            builder.ConfigureServices(services =>
            {
                foreach (var registration in services.Where(x => x.ServiceType == typeof(IHostedService) && (x.ImplementationType == typeof(PaymentWorker) || x.ImplementationType == typeof(ReservationExpiry))).ToArray()) services.Remove(registration);
                services.RemoveAll<IStripeGateway>(); services.AddSingleton<FakeStripeGateway>(); services.AddSingleton<IStripeGateway>(sp => sp.GetRequiredService<FakeStripeGateway>());
                services.PostConfigure<StripeSettings>(settings => { settings.SecretKey = "sk_test_harness_only"; settings.WebhookSecret = WebhookSecret; settings.PublicOrigin = origin; });
                services.AddSingleton<IStartupFilter>(new Driver(token, origin));
            });
        });
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, port));
        factory.StartServer();
        Console.WriteLine($"Test-only payment harness listening on {origin}");
        var finished = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; finished.TrySetResult(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => finished.TrySetResult();
        await finished.Task;
    }
}

internal sealed class Driver(string token, string origin) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextRequest) =>
        {
            if (!context.Request.Path.StartsWithSegments("/__test/payments")) { await nextRequest(); return; }
            if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Headers["X-Test-Token"].ToString() != token || context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)) { context.Response.StatusCode = 404; return; }
            var segments = context.Request.Path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 4 || !Guid.TryParse(segments[2], out var reservationId)) { context.Response.StatusCode = 400; return; }
            var db = context.RequestServices.GetRequiredService<RccDbContext>();
            var provider = context.RequestServices.GetRequiredService<FakeStripeGateway>();
            var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.ReservationId == reservationId);
            var action = segments[3];
            if (action == "drain") await PaymentWorker.RunOnceAsync(context.RequestServices.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
            else if (action == "expire")
            {
                await using var transaction = await db.Database.BeginTransactionAsync(); await Inventory.LockAsync(db);
                await db.Reservations.Where(x => x.Id == reservationId && x.Status == "PendingPayment").ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
                await Inventory.ExpireAsync(db, DateTimeOffset.UtcNow); await transaction.CommitAsync();
            }
            else if (action is "paid" or "declined" or "paid-without-webhook")
            {
                var session = action == "declined" ? provider.Sessions[payment.SessionId!] : provider.Paid(payment.SessionId!);
                if (action != "paid-without-webhook")
                {
                    var json = JsonSerializer.Serialize(new { id = "evt_" + Guid.NewGuid().ToString("N"), type = action == "declined" ? "checkout.session.async_payment_failed" : "checkout.session.completed", livemode = false, data = new { @object = new { id = session.Id, metadata = new { rcc_payment = session.PaymentId } } } });
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    using var request = new HttpRequestMessage(HttpMethod.Post, origin + "/api/payments/stripe/webhook") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(HarnessMain.WebhookSecret), Encoding.UTF8.GetBytes($"{timestamp}.{json}"))).ToLowerInvariant();
                    request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
                    using var http = new HttpClient(); using var response = await http.SendAsync(request); response.EnsureSuccessStatusCode();
                }
            }
            else if (action == "reconcile") await context.RequestServices.GetRequiredService<PaymentProcessor>().SyncAsync(payment.Id, context.RequestAborted);
            else { context.Response.StatusCode = 404; return; }
            context.Response.StatusCode = 204;
        });
        next(app);
    };
}
