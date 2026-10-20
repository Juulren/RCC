using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Services;
namespace Rcc.Api.IntegrationTests;

public sealed class NotificationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Gateway : IEmailGateway
    {
        public int Calls; public bool Fail; public Notification? Last;
        public TaskCompletionSource? Started, Continue;
        public async Task<EmailOutcome> SendAsync(Notification message, CancellationToken token)
        { Calls++; Last = message; Started?.TrySetResult(); if (Continue is not null) await Continue.Task.WaitAsync(token); if (Fail) throw new HttpRequestException(); return new("Accepted", Guid.NewGuid().ToString()); }
    }
    private async Task<Notification> Seed(Clock clock, string locale = "es")
    {
        await using var db = postgres.OpenDbContext();
        var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = Guid.NewGuid().ToString(), Email = "fixture@example.test" };
        db.Users.Add(user);
        var reservation = new Reservation { Id = Guid.NewGuid(), UserId = user.Id, Arrival = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), Departure = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(12), CreatedAt = clock.Now, ExpiresAt = clock.Now.AddMinutes(30), Total = 1760, Locale = locale };
        db.Reservations.Add(reservation); await db.SaveChangesAsync();
        await NotificationQueue.EnqueueAsync(db, reservation, "Confirmed", clock.Now); await db.SaveChangesAsync();
        await NotificationQueue.EnqueueAsync(db, reservation, "Confirmed", clock.Now); await db.SaveChangesAsync();
        return await db.Notifications.AsNoTracking().SingleAsync(x => x.ReservationId == reservation.Id);
    }
    private static IOptions<EmailSettings> Settings(bool enabled = true, string sender = "RCC <fixture@example.test>") => Options.Create(new EmailSettings { Enabled = enabled, ApiKey = "re_fixture", From = sender });
    [Theory]
    [InlineData("es", "reserva confirmada")]
    [InlineData("en", "reservation confirmed")]
    public async Task Queue_is_unique_and_preserves_recipient_locale_amount_and_dates(string locale, string subject)
    {
        var notification = await Seed(new Clock(), locale); Assert.Contains(subject, notification.Subject); Assert.Equal("fixture@example.test", notification.Recipient); Assert.Contains("1760.00 CNY", notification.Body); Assert.Contains("Asia/Shanghai", notification.Body);
    }
    [Fact]
    public async Task Disabled_email_keeps_pending_without_claiming_or_sending()
    {
        var clock = new Clock(); var message = await Seed(clock); var gateway = new Gateway(); await using var db = postgres.OpenDbContext();
        await new NotificationProcessor(db, gateway, Settings(false), clock).SendOneAsync(message.Id);
        Assert.Equal(0, gateway.Calls); Assert.Equal(0, (await db.Notifications.SingleAsync(x => x.Id == message.Id)).Attempts);
    }
    [Fact]
    public async Task Failure_retries_durably_with_same_sender_body_and_key_then_stops_on_acceptance()
    {
        var clock = new Clock(); var message = await Seed(clock); var gateway = new Gateway { Fail = true }; await using var db = postgres.OpenDbContext();
        await new NotificationProcessor(db, gateway, Settings(), clock).SendOneAsync(message.Id);
        var saved = await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == message.Id); Assert.Equal("Pending", saved.State); Assert.Equal("EMAIL_PROVIDER_UNAVAILABLE", saved.LastError); Assert.True(saved.NextAttemptAt > clock.Now);
        await new NotificationProcessor(db, gateway, Settings(), clock).SendOneAsync(message.Id); Assert.Equal(1, gateway.Calls);
        clock.Now = clock.Now.AddMinutes(2); gateway.Fail = false;
        await new NotificationProcessor(db, gateway, Settings(sender: "Other <other@example.test>"), clock).SendOneAsync(message.Id);
        Assert.Equal("RCC <fixture@example.test>", gateway.Last!.Sender); Assert.Equal(message.Body, gateway.Last.Body);
        Assert.Equal("Accepted", (await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == message.Id)).State);
        await new NotificationProcessor(db, gateway, Settings(), clock).SendOneAsync(message.Id); Assert.Equal(2, gateway.Calls);
    }
    [Fact]
    public async Task Ambiguous_email_outside_idempotency_window_requires_review_without_resending()
    {
        var clock = new Clock(); var message = await Seed(clock); var gateway = new Gateway { Fail = true }; await using var db = postgres.OpenDbContext();
        await new NotificationProcessor(db, gateway, Settings(), clock).SendOneAsync(message.Id); clock.Now = clock.Now.AddHours(23);
        await new NotificationProcessor(db, gateway, Settings(), clock).SendOneAsync(message.Id);
        Assert.Equal(1, gateway.Calls); Assert.Equal("Review", (await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == message.Id)).State);
    }
    [Fact]
    public async Task Two_workers_cannot_send_one_claim_concurrently()
    {
        var clock = new Clock(); var message = await Seed(clock); var gateway = new Gateway { Started = new(), Continue = new() };
        await using var first = postgres.OpenDbContext(); await using var second = postgres.OpenDbContext();
        var sending = new NotificationProcessor(first, gateway, Settings(), clock).SendOneAsync(message.Id);
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await new NotificationProcessor(second, gateway, Settings(), clock).SendOneAsync(message.Id); Assert.Equal(1, gateway.Calls);
        gateway.Continue.SetResult(); await sending;
    }
    [Fact]
    public async Task Expired_lease_result_cannot_regress_new_worker_acceptance()
    {
        var clock = new Clock(); var message = await Seed(clock);
        var oldGateway = new Gateway { Fail = true, Started = new(), Continue = new() };
        var newGateway = new Gateway();
        await using var oldDb = postgres.OpenDbContext(); await using var newDb = postgres.OpenDbContext();
        var oldSend = new NotificationProcessor(oldDb, oldGateway, Settings(), clock).SendOneAsync(message.Id);
        await oldGateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Now = clock.Now.AddMinutes(3);
        await new NotificationProcessor(newDb, newGateway, Settings(), clock).SendOneAsync(message.Id);
        oldGateway.Continue.SetResult(); await oldSend;
        await using var verify = postgres.OpenDbContext();
        var result = await verify.Notifications.SingleAsync(x => x.Id == message.Id);
        Assert.Equal("Accepted", result.State); Assert.Equal(2, result.Attempts); Assert.Null(result.LastError);
    }
    [Fact]
    public async Task Queue_rolls_back_with_reservation_transaction()
    {
        var clock = new Clock(); var message = await Seed(clock); await using var db = postgres.OpenDbContext();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        { var reservation = await db.Reservations.SingleAsync(x => x.Id == message.ReservationId); await NotificationQueue.EnqueueAsync(db, reservation, "Cancelled", clock.Now); await db.SaveChangesAsync(); await transaction.RollbackAsync(); }
        Assert.False(await db.Notifications.AsNoTracking().AnyAsync(x => x.ReservationId == message.ReservationId && x.Kind == "Cancelled"));
    }
    [Fact]
    public async Task Admin_queue_rejects_anonymous_and_customer_and_hides_email_content()
    {
        await using var factory = postgres.CreateFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/notifications")).StatusCode);
        using var scope = factory.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"notify-{Guid.NewGuid():N}@example.test"; const string password = "Fixture-2026!Notify"; var user = new IdentityUser { UserName = email, Email = email };
        Assert.True((await users.CreateAsync(user, password)).Succeeded); await users.AddToRoleAsync(user, "Customer");
        async Task Login() { var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) }; request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode); }
        await Login(); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/notifications")).StatusCode);
        await users.AddToRoleAsync(user, "Admin"); await Login();
        var response = await client.GetAsync("/api/admin/notifications"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore); var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("recipient", text); Assert.DoesNotContain("subject", text); Assert.DoesNotContain("providerId", text);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/notifications?page=0")).StatusCode);
    }
}
