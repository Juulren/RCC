using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcc.Api.Catalog;
using Rcc.Api.Data;
using Rcc.Api.Reservations;
using Stripe;

namespace Rcc.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapRccPayments(this WebApplication app)
    {
        app.MapPost("/api/reservations/{id:guid}/checkout", Checkout).RequireAuthorization(policy => policy.RequireRole("Customer", "Admin")).AddEndpointFilter<CatalogWriteFilter>();
        app.MapPost("/api/payments/stripe/webhook", Webhook);
        app.MapGet("/api/payments/status", (IOptions<StripeSettings> settings, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { available = settings.Value.Ready, mode = "test" });
        });
    }

    private static async Task<IResult> Checkout(Guid id, HttpContext context, RccDbContext db, TimeProvider clock, IOptions<StripeSettings> settings, PaymentProcessor processor)
    {
        context.Response.Headers.CacheControl = "no-store";
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 16 or > 100 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return CatalogEndpoints.Error(400, "IDEMPOTENCY_KEY_REQUIRED");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString())));
        Guid paymentId;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await Inventory.LockAsync(db);
            var reservation = await db.Reservations.Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id && x.UserId == user);
            if (reservation is null) return Results.NotFound();
            var prior = await db.IdempotencyRequests.SingleOrDefaultAsync(x => x.UserId == user && x.Operation == "checkout" && x.Key == key);
            if (prior is not null && prior.RequestHash != hash) return CatalogEndpoints.Error(409, "IDEMPOTENCY_CONFLICT");
            if (reservation.Status != "PendingPayment") return CatalogEndpoints.Error(409, "RESERVATION_NOT_PAYABLE");
            var now = clock.GetUtcNow();
            if (reservation.ExpiresAt <= now) return CatalogEndpoints.Error(409, "RESERVATION_EXPIRED");
            if (!settings.Value.Ready) return CatalogEndpoints.Error(503, "PAYMENTS_NOT_CONFIGURED");
            if (reservation.ExpiresAt - now < TimeSpan.FromMinutes(1)) return CatalogEndpoints.Error(409, "CHECKOUT_WINDOW_TOO_SHORT");
            if (reservation.Total * 100 is < 1 or > 99999999) return CatalogEndpoints.Error(409, "PAYMENT_AMOUNT_LIMIT");
            var payment = reservation.Payments.SingleOrDefault();
            if (payment is null)
            {
                payment = new Payment { Id = Guid.NewGuid(), ReservationId = id, AmountMinor = checked((long)(reservation.Total * 100)), ReturnOrigin = settings.Value.PublicOrigin.TrimEnd('/'), CreatedAt = now, CheckoutExpiresAt = DateTimeOffset.FromUnixTimeSeconds(now.AddMinutes(31).ToUnixTimeSeconds()), NextSyncAt = now.AddMinutes(1) };
                db.Payments.Add(payment);
            }
            if (payment.State is "Review" or "Refunded" or "Expired" or "Paid") return CatalogEndpoints.Error(409, "PAYMENT_REQUIRES_REVIEW");
            if (prior is null) db.IdempotencyRequests.Add(new IdempotencyRequest { Id = Guid.NewGuid(), UserId = user, Operation = "checkout", Key = key, RequestHash = hash, ResourceId = id, CreatedAt = now });
            paymentId = payment.Id;
            await db.SaveChangesAsync(); await transaction.CommitAsync();
        }
        try
        {
            var session = await processor.SyncAsync(paymentId, context.RequestAborted);
            db.ChangeTracker.Clear();
            var payment = await db.Payments.AsNoTracking().Include(x => x.Reservation).SingleAsync(x => x.Id == paymentId);
            if (payment.State == "Review") return CatalogEndpoints.Error(409, "PAYMENT_REQUIRES_REVIEW");
            if (payment.Reservation.Status == "Confirmed") return Results.Ok(new { status = "Confirmed", url = (string?)null, reservationId = id });
            if (payment.Reservation.Status != "PendingPayment" || payment.Reservation.ExpiresAt <= clock.GetUtcNow()) return CatalogEndpoints.Error(409, "RESERVATION_EXPIRED");
            if (session.Status != "open" || session.PaymentStatus == "paid" || session.Url is null) return CatalogEndpoints.Error(409, "RESERVATION_NOT_PAYABLE");
            return Results.Ok(new { status = "Open", session.Url, reservationId = id, expiresAt = payment.Reservation.ExpiresAt, mode = "test" });
        }
        catch (Exception error) when (error is StripeException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await processor.RecordError(paymentId, "PAYMENT_PROVIDER_UNAVAILABLE");
            return CatalogEndpoints.Error(503, "PAYMENT_PROVIDER_UNAVAILABLE");
        }
    }

    private static async Task<IResult> Webhook(HttpContext context, RccDbContext db, TimeProvider clock, IOptions<StripeSettings> settings, IStripeGateway stripe, PaymentProcessor processor)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!settings.Value.Ready) return CatalogEndpoints.Error(503, "PAYMENTS_NOT_CONFIGURED");
        if (context.Request.ContentLength > 262144) return Results.StatusCode(413);
        string id, type, subject; Guid? paymentHint = null;
        try
        {
            using var reader = new StreamReader(context.Request.Body, new UTF8Encoding(false, true));
            var buffer = new char[262145]; var length = 0;
            while (length < buffer.Length)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(length), context.RequestAborted);
                if (read == 0) break; length += read;
            }
            if (length > 262144) return Results.StatusCode(413);
            var raw = new string(buffer, 0, length);
            EventUtility.ValidateSignature(raw, context.Request.Headers["Stripe-Signature"].ToString(), settings.Value.WebhookSecret, 300, clock.GetUtcNow().ToUnixTimeSeconds());
            using var document = JsonDocument.Parse(raw); var root = document.RootElement;
            id = root.GetProperty("id").GetString()!; type = root.GetProperty("type").GetString()!;
            if (id is null || !id.StartsWith("evt_", StringComparison.Ordinal) || id.Length > 255 || type is null || type.Length > 100 || root.GetProperty("livemode").GetBoolean()) return CatalogEndpoints.Error(400, "WEBHOOK_INVALID");
            var obj = root.GetProperty("data").GetProperty("object");
            subject = obj.GetProperty("id").GetString()!;
            if (subject is null || subject.Length > 255) return CatalogEndpoints.Error(400, "WEBHOOK_INVALID");
            if (obj.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("rcc_payment", out var hint) && hint.ValueKind == JsonValueKind.String && Guid.TryParse(hint.GetString(), out var guid)) paymentHint = guid;
        }
        catch (Exception error) when (error is StripeException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException or OverflowException) { return CatalogEndpoints.Error(400, "WEBHOOK_INVALID"); }
        if (await db.PaymentEvents.AnyAsync(x => x.Id == id)) return Results.Ok(new { received = true });
        var supported = type is "checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed" or "checkout.session.expired";
        if (supported && !subject.StartsWith("cs_test_", StringComparison.Ordinal)) return CatalogEndpoints.Error(400, "WEBHOOK_INVALID");
        var paymentId = supported ? await db.Payments.AsNoTracking().Where(x => x.SessionId == subject).Select(x => (Guid?)x.Id).SingleOrDefaultAsync() : null;
        if (supported && paymentId is null && paymentHint.HasValue) paymentId = await db.Payments.AsNoTracking().Where(x => x.Id == paymentHint.Value).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
        if (paymentId is not null)
        {
            try
            {
                var snapshot = await stripe.GetAsync(subject, context.RequestAborted);
                await processor.ApplyAsync(paymentId.Value, snapshot, id, type);
            }
            catch (Exception error) when (error is StripeException or HttpRequestException or TaskCanceledException)
            { return CatalogEndpoints.Error(503, "PAYMENT_PROVIDER_UNAVAILABLE"); }
        }
        else
        {
            await using var transaction = await db.Database.BeginTransactionAsync(); await Inventory.LockAsync(db);
            if (!await db.PaymentEvents.AnyAsync(x => x.Id == id)) { db.PaymentEvents.Add(new PaymentEvent { Id = id, Type = type, ReceivedAt = clock.GetUtcNow(), Outcome = "Ignored" }); await db.SaveChangesAsync(); }
            await transaction.CommitAsync();
        }
        return Results.Ok(new { received = true });
    }
}
