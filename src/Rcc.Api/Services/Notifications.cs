using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Reservations;
namespace Rcc.Api.Services;

public sealed class EmailSettings
{
    public string ApiKey { get; set; } = "";
    public string From { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Ready => Enabled && ApiKey.StartsWith("re_", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(From) && !From.Contains('\r') && !From.Contains('\n');
}
public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string Kind { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Sender { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? FirstAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? ProviderId { get; set; }
    public string? LastError { get; set; }
}
public static class NotificationQueue
{
    // Called inside the reservation transaction; no network I/O here.
    public static async Task EnqueueAsync(RccDbContext db, Reservation reservation, string kind, DateTimeOffset now)
    {
        if (await db.Notifications.AnyAsync(x => x.ReservationId == reservation.Id && x.Kind == kind)) return;
        var email = await db.Users.Where(x => x.Id == reservation.UserId).Select(x => x.Email).SingleAsync();
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Reservation owner has no email.");
        var es = reservation.Locale == "es";
        var confirmed = kind == "Confirmed";
        var subject = es ? (confirmed ? "RCC: reserva confirmada" : "RCC: reserva cancelada") : (confirmed ? "RCC: reservation confirmed" : "RCC: reservation cancelled");
        var body = $"{subject}\n{(es ? "Referencia" : "Reference")}: {reservation.Id}\n{reservation.Arrival:yyyy-MM-dd} / {reservation.Departure:yyyy-MM-dd} (Asia/Shanghai)\n{reservation.Total.ToString("0.00", CultureInfo.InvariantCulture)} CNY\n{(es ? "Proyecto académico. Pago de prueba. Consulta el estado actual y el reembolso en Mis reservas." : "Academic project. Test payment. Check current status and refund progress in My reservations.")}\n{now:O}";
        db.Notifications.Add(new() { Id = Guid.NewGuid(), ReservationId = reservation.Id, Kind = kind, Recipient = email, Subject = subject, Body = body, CreatedAt = now, NextAttemptAt = now });
    }
}
public sealed record EmailOutcome(string State, string? ProviderId = null, string? Error = null);
public interface IEmailGateway { Task<EmailOutcome> SendAsync(Notification notification, CancellationToken token); }
public sealed class ResendGateway(IHttpClientFactory clients, IOptions<EmailSettings> settings) : IEmailGateway
{
    public async Task<EmailOutcome> SendAsync(Notification notification, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.ApiKey);
        request.Headers.Add("Idempotency-Key", $"rcc-notification-{notification.Id:N}");
        request.Content = JsonContent.Create(new { from = notification.Sender, to = new[] { notification.Recipient }, subject = notification.Subject, text = notification.Body });
        using var response = await clients.CreateClient("email").SendAsync(request, token);
        if (!response.IsSuccessStatusCode) return new((int)response.StatusCode is 429 or >= 500 ? "Pending" : "Review", Error: "EMAIL_PROVIDER_" + (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var id = json.RootElement.GetProperty("id").GetString();
        return Guid.TryParse(id, out _) ? new("Accepted", id) : new("Pending", Error: "EMAIL_RESPONSE_INVALID");
    }
}
public sealed class NotificationProcessor(RccDbContext db, IEmailGateway gateway, IOptions<EmailSettings> settings, TimeProvider clock)
{
    public async Task SendOneAsync(Guid id, CancellationToken token = default)
    {
        if (!settings.Value.Ready) return;
        db.ChangeTracker.Clear();
        Notification snapshot;
        await using (var transaction = await db.Database.BeginTransactionAsync(token))
        {
            await Inventory.LockAsync(db);
            var notification = await db.Notifications.SingleAsync(x => x.Id == id, token);
            var now = clock.GetUtcNow();
            if (notification.State != "Pending" || notification.NextAttemptAt > now || notification.LeaseUntil > now) return;
            // Resend retains idempotency keys for 24h. Never retry an ambiguous result outside that window.
            if (notification.FirstAttemptAt is not null && now - notification.FirstAttemptAt >= TimeSpan.FromHours(23))
            { notification.State = "Review"; notification.LastError = "EMAIL_IDEMPOTENCY_WINDOW"; }
            else
            {
                notification.Sender = notification.FirstAttemptAt is null ? settings.Value.From : notification.Sender;
                notification.FirstAttemptAt ??= now; notification.Attempts++; notification.LeaseUntil = now.AddMinutes(2);
            }
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
            if (notification.State != "Pending") return;
            snapshot = notification;
        }
        EmailOutcome outcome;
        try { outcome = await gateway.SendAsync(snapshot, token); }
        catch (Exception error) when (error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException || error is OperationCanceledException && !token.IsCancellationRequested)
        { outcome = new("Pending", Error: "EMAIL_PROVIDER_UNAVAILABLE"); }
        db.ChangeTracker.Clear();
        await using var save = await db.Database.BeginTransactionAsync(token);
        await Inventory.LockAsync(db);
        var record = await db.Notifications.SingleAsync(x => x.Id == id, token);
        // Monotonic attempt number fences late results without timestamp precision assumptions.
        if (record.State != "Pending" || record.Attempts != snapshot.Attempts) return;
        record.State = outcome.State; record.ProviderId = outcome.ProviderId; record.LastError = outcome.Error; record.LeaseUntil = null;
        record.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(record.Attempts, 7))));
        await db.SaveChangesAsync(token); await save.CommitAsync(token);
    }
}
public sealed class NotificationWorker(IServiceScopeFactory scopes, IOptions<EmailSettings> settings, TimeProvider clock, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!settings.Value.Ready) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<RccDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationProcessor>();
                var now = clock.GetUtcNow();
                var ids = await db.Notifications.AsNoTracking().Where(x => x.State == "Pending" && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now)).OrderBy(x => x.CreatedAt).Take(20).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in ids) await processor.SendOneAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Notification cycle failed ({ErrorType}); retrying next cycle.", error.GetType().Name); }
        }
    }
}
