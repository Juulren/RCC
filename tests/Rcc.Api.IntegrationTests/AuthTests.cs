using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Rcc.Api.Auth;

namespace Rcc.Api.IntegrationTests;

public sealed class AuthTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Rcc-Test-2026!Strong";
    private static string Email() => $"rcc-{Guid.NewGuid():N}@example.test";

    private static async Task<string> Token(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/" + path)
        { Content = JsonContent.Create(body ?? new { }) };
        request.Headers.Add("X-CSRF-TOKEN", token ?? await Token(client));
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Register_requires_matching_csrf_and_does_not_create_user_on_rejection()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var email = Email();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "register", new { email, password = Password }, "forged")).StatusCode);
        using var other = factory.CreateClient();
        var otherToken = await Token(other);
        await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "register", new { email, password = Password }, otherToken)).StatusCode);
        await using var database = postgres.OpenDbContext();
        Assert.False(await database.Users.AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task Registration_hashes_password_assigns_only_customer_and_rejects_case_insensitive_duplicate()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var email = Email();
        var response = await Post(client, "register", new { email, password = Password, roles = new[] { "Admin" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "register", new { email = email.ToUpperInvariant(), password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, Password));
        Assert.Equal(new[] { "Customer" }, await users.GetRolesAsync(user));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Rcc-Test-2026!Strong")]
    [InlineData("weak@example.test", "short")]
    [InlineData("weak@example.test", "onlylowercasepassword")]
    public async Task Registration_validates_email_and_password(string email, string password)
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "register", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_registration_preserves_unique_email_and_returns_conflict()
    {
        await using var factory = postgres.CreateFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var email = Email();
        var responses = await Task.WhenAll(
            Post(first, "register", new { email, password = Password }),
            Post(second, "register", new { email = email.ToUpperInvariant(), password = Password }));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var database = postgres.OpenDbContext();
        Assert.Equal(1, await database.Users.CountAsync(user => user.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Login_session_csrf_rotation_logout_and_replayed_cookie_invalidation()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var email = Email();
        await Post(client, "register", new { email, password = Password });
        var anonymousToken = await Token(client);
        var login = await Post(client, "login", new { email = email.ToUpperInvariant(), password = Password }, anonymousToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var sessionCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("Rcc.Session="));
        Assert.Contains("httponly", sessionCookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", sessionCookie.ToLowerInvariant());
        Assert.DoesNotContain("expires=", sessionCookie.ToLowerInvariant());
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "logout", token: anonymousToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", sessionCookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_are_generic_and_five_failures_lock_account()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        var email = Email();
        await Post(client, "register", new { email, password = Password });
        var unknown = await Post(client, "login", new { email = Email(), password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await Post(client, "login", new { email, password = "Incorrect-password!" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal("INVALID_CREDENTIALS", (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "login", new { email, password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByEmailAsync(email))!));
    }

    [Fact]
    public async Task Admin_endpoint_distinguishes_anonymous_customer_and_admin()
    {
        await using var factory = postgres.CreateFactory();
        using var customer = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/admin/access")).StatusCode);
        var email = Email();
        await Post(customer, "register", new { email, password = Password });
        await Post(customer, "login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/access")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var adminEmail = Email();
        var admin = new IdentityUser { Email = adminEmail, UserName = adminEmail };
        Assert.True((await users.CreateAsync(admin, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        using var administrator = factory.CreateClient();
        await Post(administrator, "login", new { email = adminEmail, password = Password });
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync("/api/admin/access")).StatusCode);
    }

    [Fact]
    public async Task Auth_endpoints_rate_limit_repeated_attempts()
    {
        await using var factory = postgres.CreateFactory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 12; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "login", new { email = Email(), password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, "login", new { email = Email(), password = Password })).StatusCode);
    }

    [Fact]
    public async Task Production_cookies_require_https_httponly_and_host_prefix()
    {
        var productionSettings = ProductionTests.Settings();
        productionSettings["Deployment:PublicOrigin"] = "https://localhost";
        await using var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            foreach (var pair in productionSettings) builder.UseSetting(pair.Key, pair.Value);
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var email = Email();
        var csrf = await client.GetAsync("/api/auth/csrf");
        var csrfCookie = csrf.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-Rcc.Csrf="));
        Assert.Contains("secure", csrfCookie.ToLowerInvariant());
        Assert.Contains("httponly", csrfCookie.ToLowerInvariant());
        await Post(client, "register", new { email, password = Password });
        var login = await Post(client, "login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-Rcc.Session="));
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
    }

    [Fact]
    public async Task Admin_bootstrap_is_idempotent_and_never_promotes_existing_customer()
    {
        await using var factory = postgres.CreateFactory();
        var email = Email();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Administrator:Email"] = email, ["Administrator:Password"] = Password }).Build();
        await IdentityBootstrap.InitializeAsync(factory.Services, config);
        await IdentityBootstrap.InitializeAsync(factory.Services, config);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync(email))!, "Admin"));
        using var client = factory.CreateClient();
        var customer = Email();
        await Post(client, "register", new { email = customer, password = Password });
        config["Administrator:Email"] = customer;
        await Assert.ThrowsAsync<InvalidOperationException>(() => IdentityBootstrap.InitializeAsync(factory.Services, config));
        Assert.False(await users.IsInRoleAsync((await users.FindByEmailAsync(customer))!, "Admin"));
    }
}
