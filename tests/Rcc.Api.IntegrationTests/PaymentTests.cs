using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Rcc.Api.Data;
using Rcc.Api.Payments;
using Rcc.Api.Reservations;
using Rcc.Testing;

namespace Rcc.Api.IntegrationTests;

public sealed class PaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = postgres.OpenDbContext();
        if (!System.Text.RegularExpressions.Regex.IsMatch(db.Database.GetDbConnection().Database, "^rcc_test_[0-9a-f]{32}$")) throw new InvalidOperationException("Not a fixture-owned test database.");
        // Each scenario has its own provider account in memory. Its database must
        // contain only that scenario's monetary work when the global worker runs.
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"PaymentEvents\", \"RefundJobs\", \"Payments\", \"IdempotencyRequests\", \"ReservationItems\", \"Reservations\"");
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private const string Secret = "whsec_test_fixture_only_not_a_provider_secret";
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private WebApplicationFactory<Program> Factory(FakeStripeGateway fake, Clock? clock = null, bool configured = true) => postgres.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        foreach (var registration in services.Where(x => x.ServiceType == typeof(IHostedService) && (x.ImplementationType == typeof(PaymentWorker) || x.ImplementationType == typeof(ReservationExpiry))).ToArray()) services.Remove(registration);
        services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock ?? new Clock());
        services.RemoveAll<IStripeGateway>(); services.AddSingleton<IStripeGateway>(fake);
        services.PostConfigure<StripeSettings>(options => { options.SecretKey = configured ? "sk_test_fixture_only" : ""; options.WebhookSecret = Secret; options.PublicOrigin = "http://localhost:5080"; });
    }));
    private static async Task<HttpClient> Client(WebApplicationFactory<Program> factory, bool admin = false)
    {
        using var scope = factory.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"payment-{Guid.NewGuid():N}@example.test"; const string password = "Payment-2026!Fixture";
        var user = new IdentityUser { UserName = email, Email = email }; Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, admin ? "Admin" : "Customer")).Succeeded);
        var client = factory.CreateClient(); Assert.Equal(HttpStatusCode.NoContent, (await Write(client, "/auth/login", new { email, password })).StatusCode); return client;
    }
    private static async Task<HttpResponseMessage> Write(HttpClient client, string path, object body, string? key = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api" + path) { Content = JsonContent.Create(body) };
        if (csrf) { var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString()); }
        if (key is not null) request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request);
    }
    private async Task<Guid> Reservation(HttpClient client, Clock? clock = null, decimal rate = 100m)
    {
        await using var db = postgres.OpenDbContext(); var type = new RoomType { Id = Guid.NewGuid(), HotelId = CatalogSeed.HotelId, Slug = "pay-" + Guid.NewGuid().ToString("N"), NameEs = "Prueba", NameEn = "Test", DescriptionEs = "Prueba", DescriptionEn = "Test", Category = "room", Beds = "king", Area = 60, Capacity = 3, MaxAdults = 2, MaxChildren = 1, NightlyRate = rate };
        type.Rooms = [new PhysicalRoom { Id = Guid.NewGuid(), HotelId = type.HotelId, RoomTypeId = type.Id, Number = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() }];
        db.RoomTypes.Add(type); await db.SaveChangesAsync();
        var today = Inventory.Today(clock ?? new Clock()); var stay = new StayInput(today.AddDays(10), today.AddDays(12), [new GuestInput(2, [], type.Id)]);
        var quote = (await (await client.PostAsJsonAsync("/api/quotes", stay)).Content.ReadFromJsonAsync<QuoteView>())!;
        var result = await Write(client, "/reservations", new ReserveInput(stay.Arrival, stay.Departure, stay.Rooms, quote.PriceFingerprint, "es"), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, result.StatusCode); return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static Task<HttpResponseMessage> Checkout(HttpClient client, Guid id, string? key = null, bool csrf = true) => Write(client, $"/reservations/{id}/checkout", new { }, key ?? Guid.NewGuid().ToString("N"), csrf);
    private async Task<Payment> Payment(Guid reservationId)
    {
        await using var db = postgres.OpenDbContext(); return await db.Payments.AsNoTracking().SingleAsync(x => x.ReservationId == reservationId);
    }
    private static string Signature(string body, DateTimeOffset now) => $"t={now.ToUnixTimeSeconds()},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{now.ToUnixTimeSeconds()}.{body}"))).ToLowerInvariant()}";
    private static async Task<HttpResponseMessage> Event(HttpClient client, StripeSession session, Clock? clock = null, string? eventId = null, string type = "checkout.session.completed", bool badSignature = false, bool live = false, DateTimeOffset? timestamp = null)
    {
        var body = JsonSerializer.Serialize(new { id = eventId ?? "evt_" + Guid.NewGuid().ToString("N"), type, livemode = live, data = new { @object = new { id = session.Id, metadata = new { rcc_payment = session.PaymentId } } } });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Stripe-Signature", badSignature ? "t=1,v1=bad" : Signature(body, timestamp ?? (clock?.GetUtcNow() ?? DateTimeOffset.UtcNow)));
        return await client.SendAsync(request);
    }
    private static async Task Drain(WebApplicationFactory<Program> factory, Clock clock) => await PaymentWorker.RunOnceAsync(factory.Services.GetRequiredService<IServiceScopeFactory>(), clock);

    [Fact]
    public async Task Checkout_requires_owner_csrf_key_and_test_configuration()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake, configured: false); using var client = await Client(factory); using var stranger = await Client(factory); using var visitor = factory.CreateClient();
        var id = await Reservation(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Checkout(visitor, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Checkout(stranger, id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Checkout(client, id, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, $"/reservations/{id}/checkout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, id)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/payments/status")).GetProperty("available").GetBoolean());
        Assert.Equal(0, fake.CreateCalls);
    }

    [Fact]
    public async Task Checkout_reuses_one_session_and_keys_without_extending_inventory_or_accepting_amounts()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory);
        var id = await Reservation(client, clock); var key = Guid.NewGuid().ToString("N");
        var first = await Write(client, $"/reservations/{id}/checkout", new { amount = 1, currency = "usd", returnUrl = "https://attacker.invalid" }, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await client.GetFromJsonAsync<JsonElement>($"/api/reservations/{id}");
        clock.Now = clock.Now.AddMinutes(5);
        Assert.Equal(HttpStatusCode.OK, (await Checkout(client, id, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Checkout(client, id)).StatusCode);
        var payment = await Payment(id); Assert.Equal(20000, payment.AmountMinor); Assert.Equal("cny", payment.Currency);
        Assert.Single(fake.Sessions); Assert.Equal(1, fake.CreateCalls);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/reservations/{id}"); Assert.Equal(original.GetProperty("expiresAt").GetDateTimeOffset(), detail.GetProperty("expiresAt").GetDateTimeOffset());
        var secondId = await Reservation(client, clock); Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, secondId, key)).StatusCode);
        Assert.DoesNotContain("cs_test_", detail.GetRawText()); Assert.DoesNotContain("sk_test", detail.GetRawText());
    }

    [Fact]
    public async Task Lost_provider_response_recovers_same_checkout_after_retry()
    {
        var fake = new FakeStripeGateway { LoseCreateResponse = true }; await using var factory = Factory(fake); using var client = await Client(factory);
        var id = await Reservation(client); var key = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Checkout(client, id, key)).StatusCode);
        Assert.Null((await Payment(id)).SessionId); Assert.Single(fake.Sessions);
        Assert.Equal(HttpStatusCode.OK, (await Checkout(client, id, key)).StatusCode); Assert.Single(fake.Sessions);
        Assert.Equal(fake.Sessions.Single().Key, (await Payment(id)).SessionId);
    }

    [Fact]
    public async Task Parallel_checkout_requests_share_canonical_provider_identity()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client);
        var results = await Task.WhenAll(Checkout(client, id), Checkout(client, id));
        Assert.All(results, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode)); Assert.Single(fake.Sessions);
        await using var db = postgres.OpenDbContext(); Assert.Equal(1, await db.Payments.CountAsync(x => x.ReservationId == id));
    }

    [Fact]
    public async Task Verified_paid_webhook_confirms_once_and_browser_return_never_confirms()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id);
        var returnPage = await client.GetAsync($"/mis-reservas?reserva={id}&pago=retorno"); Assert.Equal(HttpStatusCode.OK, returnPage.StatusCode);
        await using var db = postgres.OpenDbContext(); Assert.Equal("PendingPayment", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
        var session = fake.Paid((await Payment(id)).SessionId!); var eventId = "evt_" + Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Event(client, session, eventId: eventId), Event(client, session, eventId: eventId));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, await db.PaymentEvents.CountAsync(x => x.Id == eventId));
        Assert.Equal("Confirmed", await db.Reservations.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());
        Assert.Equal("Paid", (await Payment(id)).State);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("reference")]
    [InlineData("live")]
    [InlineData("intentAmount")]
    [InlineData("intentStatus")]
    public async Task Provider_snapshot_mismatch_never_confirms(string mutation)
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id);
        var session = fake.Paid((await Payment(id)).SessionId!);
        session = mutation switch { "amount" => session with { Amount = 1 }, "currency" => session with { Currency = "usd" }, "reference" => session with { ReservationId = Guid.NewGuid().ToString() }, "live" => session with { Live = true }, "intentAmount" => session with { IntentReceived = 1 }, _ => session with { IntentStatus = "processing" } };
        fake.Sessions[session.Id] = session;
        Assert.Equal(HttpStatusCode.OK, (await Event(client, session)).StatusCode);
        await using var db = postgres.OpenDbContext(); Assert.Equal("PendingPayment", (await db.Reservations.SingleAsync(x => x.Id == id)).Status); Assert.Equal("Review", (await Payment(id)).State);
    }

    [Fact]
    public async Task Invalid_signature_old_timestamp_live_event_and_oversized_body_are_rejected()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id);
        var session = fake.Paid((await Payment(id)).SessionId!);
        Assert.Equal(HttpStatusCode.BadRequest, (await Event(client, session, badSignature: true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Event(client, session, timestamp: DateTimeOffset.UtcNow.AddMinutes(-6))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Event(client, session, live: true)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/payments/stripe/webhook", new StringContent(new string('x', 262145)))).StatusCode);
        await using var db = postgres.OpenDbContext(); Assert.Equal("PendingPayment", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task Failed_attempt_keeps_hold_and_later_success_can_confirm()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id);
        var session = fake.Sessions[(await Payment(id)).SessionId!];
        await Event(client, session, type: "checkout.session.async_payment_failed");
        await using var db = postgres.OpenDbContext(); Assert.Equal("PendingPayment", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Checkout(client, id)).StatusCode);
        await Event(client, fake.Paid(session.Id)); Assert.Equal("Paid", (await Payment(id)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_payment_after_expiry_or_cancellation_queues_full_refund(bool cancel)
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory); var id = await Reservation(client, clock); await Checkout(client, id);
        if (cancel) await Write(client, $"/reservations/{id}/cancel", new { }); else clock.Now = clock.Now.AddMinutes(30);
        var session = fake.Paid((await Payment(id)).SessionId!); await Event(client, session, clock);
        await Drain(factory, clock);
        await using var db = postgres.OpenDbContext(); var saved = await db.Reservations.Include(x => x.Payments).ThenInclude(x => x.Refund).SingleAsync(x => x.Id == id);
        Assert.Equal(cancel ? "Cancelled" : "Expired", saved.Status); Assert.Equal("Succeeded", saved.Payments.Single().Refund!.State);
        Assert.Equal(20000, saved.Payments.Single().Refund!.AmountMinor); Assert.Single(fake.Refunds);
        var roomId = (await db.ReservationItems.SingleAsync(x => x.ReservationId == id)).RoomId;
        Assert.False(await Inventory.Committed(db, clock.Now).AnyAsync(x => x.RoomId == roomId));
    }

    [Fact]
    public async Task Confirmed_cancellation_is_atomic_idempotent_and_refund_retries_after_lost_response()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway { LoseRefundResponse = true }; await using var factory = Factory(fake, clock); using var client = await Client(factory); var id = await Reservation(client, clock); await Checkout(client, id);
        await Event(client, fake.Paid((await Payment(id)).SessionId!), clock);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        await Drain(factory, clock); Assert.Single(fake.Refunds);
        clock.Now = clock.Now.AddMinutes(2); await Drain(factory, clock);
        await using var db = postgres.OpenDbContext(); var refunds = await db.RefundJobs.Where(x => x.Payment.ReservationId == id).ToListAsync();
        Assert.Single(refunds); Assert.Equal("Succeeded", refunds[0].State); Assert.Single(fake.Refunds); Assert.Equal(2, fake.RefundCalls);
    }

    [Fact]
    public async Task Pending_refund_is_not_reported_successful_and_terminal_failure_needs_review()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway { RefundStatus = "pending" }; await using var factory = Factory(fake, clock); using var client = await Client(factory); var id = await Reservation(client, clock); await Checkout(client, id); await Event(client, fake.Paid((await Payment(id)).SessionId!), clock); await Write(client, $"/reservations/{id}/cancel", new { });
        await Drain(factory, clock); var refund = fake.Refunds.Single(); fake.Refunds[refund.Key] = refund.Value with { Status = "failed" };
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/reservations/{id}"); Assert.Equal("Processing", detail.GetProperty("payments")[0].GetProperty("refund").GetProperty("state").GetString());
        clock.Now = clock.Now.AddMinutes(2); await Drain(factory, clock);
        await using var db = postgres.OpenDbContext(); var job = await db.RefundJobs.SingleAsync(x => x.Payment.ReservationId == id); Assert.Equal("Failed", job.State); Assert.Equal("REFUND_FAILED", job.LastError); Assert.Equal("Cancelled", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task Missing_webhook_is_reconciled_from_authenticated_provider_read()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory); var id = await Reservation(client, clock); await Checkout(client, id); fake.Paid((await Payment(id)).SessionId!);
        clock.Now = clock.Now.AddMinutes(2); await Drain(factory, clock);
        await using var db = postgres.OpenDbContext(); Assert.Equal("Confirmed", (await db.Reservations.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task Webhook_provider_outage_is_retryable_and_not_consumed()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id); var session = fake.Paid((await Payment(id)).SessionId!); var eventId = "evt_" + Guid.NewGuid().ToString("N");
        fake.FailReads = true; Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Event(client, session, eventId: eventId)).StatusCode);
        await using var db = postgres.OpenDbContext(); Assert.False(await db.PaymentEvents.AnyAsync(x => x.Id == eventId));
        fake.FailReads = false; Assert.Equal(HttpStatusCode.OK, (await Event(client, session, eventId: eventId)).StatusCode);
    }

    [Fact]
    public async Task Cancellation_wins_during_inflight_verification_and_never_resurrects_inventory()
    {
        var fake = new FakeStripeGateway(); await using var factory = Factory(fake); using var client = await Client(factory); var id = await Reservation(client); await Checkout(client, id); var session = fake.Paid((await Payment(id)).SessionId!);
        fake.ReadStarted = new(); fake.ResumeRead = new(); var callback = Event(client, session);
        await fake.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, (await Write(client, $"/reservations/{id}/cancel", new { })).StatusCode);
        fake.ResumeRead.TrySetResult(); Assert.Equal(HttpStatusCode.OK, (await callback).StatusCode);
        await using var db = postgres.OpenDbContext(); Assert.Equal("Cancelled", (await db.Reservations.SingleAsync(x => x.Id == id)).Status); Assert.Equal(1, await db.RefundJobs.CountAsync(x => x.Payment.ReservationId == id));
    }

    [Fact]
    public async Task Too_little_time_and_excessive_amount_do_not_create_checkout()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory);
        var expensive = await Reservation(client, clock, 9999999m); Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, expensive)).StatusCode);
        var id = await Reservation(client, clock); clock.Now = clock.Now.AddMinutes(29.5);
        Assert.Equal(HttpStatusCode.Conflict, (await Checkout(client, id)).StatusCode); Assert.Empty(fake.Sessions);
    }

    [Fact]
    public async Task Paid_snapshot_after_resale_refunds_original_without_affecting_new_assignment()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory);
        var originalId = await Reservation(client, clock); await Checkout(client, originalId);
        await using var db = postgres.OpenDbContext();
        var original = await db.Reservations.Include(x => x.Items).SingleAsync(x => x.Id == originalId);
        var physical = await db.Rooms.SingleAsync(x => x.Id == original.Items[0].RoomId);
        clock.Now = clock.Now.AddMinutes(30);
        var stay = new StayInput(original.Arrival, original.Departure, [new GuestInput(2, [], physical.RoomTypeId)]);
        var quote = (await (await client.PostAsJsonAsync("/api/quotes", stay)).Content.ReadFromJsonAsync<QuoteView>())!;
        var created = await Write(client, "/reservations", new ReserveInput(stay.Arrival, stay.Departure, stay.Rooms, quote.PriceFingerprint, "es"), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var nextId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await Event(client, fake.Paid((await Payment(originalId)).SessionId!), clock); await Drain(factory, clock);
        Assert.Equal("PendingPayment", await db.Reservations.Where(x => x.Id == nextId).Select(x => x.Status).SingleAsync());
        Assert.True(await Inventory.Committed(db, clock.Now).AnyAsync(x => x.ReservationId == nextId && x.RoomId == physical.Id));
        Assert.Equal("Expired", await db.Reservations.Where(x => x.Id == originalId).Select(x => x.Status).SingleAsync());
        Assert.Single(fake.Refunds);
    }

    [Fact]
    public async Task Old_unpaid_snapshot_does_not_regress_paid_and_duplicate_success_does_not_double_refund()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway(); await using var factory = Factory(fake, clock); using var client = await Client(factory);
        var id = await Reservation(client, clock); await Checkout(client, id); var payment = await Payment(id);
        var old = fake.Sessions[payment.SessionId!]; var paid = fake.Paid(payment.SessionId!);
        await Event(client, paid, clock);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<PaymentProcessor>().ApplyAsync(payment.Id, old with { Status = "expired" });
        Assert.Equal("Paid", (await Payment(id)).State);
        await Write(client, $"/reservations/{id}/cancel", new { }); await Drain(factory, clock);
        await Event(client, paid, clock); await Drain(factory, clock);
        Assert.Single(fake.Refunds); Assert.Equal("Refunded", (await Payment(id)).State);
    }

    [Fact]
    public async Task Unknown_refund_result_beyond_provider_idempotency_window_requires_review()
    {
        var clock = new Clock(); var fake = new FakeStripeGateway { FailRefunds = true }; await using var factory = Factory(fake, clock); using var client = await Client(factory);
        var id = await Reservation(client, clock); await Checkout(client, id); await Event(client, fake.Paid((await Payment(id)).SessionId!), clock); await Write(client, $"/reservations/{id}/cancel", new { }); await Drain(factory, clock);
        clock.Now = clock.Now.AddHours(24); fake.FailRefunds = false; await Drain(factory, clock);
        await using var db = postgres.OpenDbContext(); var refund = await db.RefundJobs.SingleAsync(x => x.Payment.ReservationId == id);
        Assert.Equal("Review", refund.State); Assert.Equal("REFUND_REQUIRES_REVIEW", refund.LastError); Assert.Empty(fake.Refunds);
    }

    [Fact]
    public async Task Webhook_before_checkout_response_confirms_using_durable_metadata_without_expiring_paid_session()
    {
        var fake = new FakeStripeGateway { CreateStarted = new(), ResumeCreate = new() }; await using var factory = Factory(fake); using var client = await Client(factory);
        var id = await Reservation(client); var checkout = Checkout(client, id);
        await fake.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var session = fake.Paid(fake.Sessions.Single().Key);
        Assert.Equal(HttpStatusCode.OK, (await Event(client, session)).StatusCode);
        fake.ResumeCreate.TrySetResult(); var response = await checkout;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Confirmed", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("url").ValueKind); Assert.Equal(0, fake.ExpireCalls); Assert.Equal("Paid", (await Payment(id)).State);
    }
}
