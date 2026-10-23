using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rcc.Api.Data;
namespace Rcc.Api.IntegrationTests;

public sealed class ProductionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Dictionary<string, string?> CommonSettings = CreateSettings();
    internal static Dictionary<string, string?> Settings() => new(CommonSettings);
    private static Dictionary<string, string?> CreateSettings()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=RCC test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        return new() { ["Deployment:PublicOrigin"] = "https://rcc.example.test", ["DataProtection:CertificateBase64"] = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password)), ["DataProtection:CertificatePassword"] = password };
    }
    private WebApplicationFactory<Program> Factory(Dictionary<string, string?> settings) => postgres.CreateFactory().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Production");
        foreach (var pair in settings) builder.UseSetting(pair.Key, pair.Value);
    });
    [Fact]
    public async Task Production_csrf_cookie_is_secure_and_unknown_host_is_rejected()
    {
        await using var factory = Factory(Settings());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://rcc.example.test"), AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-Rcc.Csrf", cookie); Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie);
        using var hostile = factory.CreateClient(new() { BaseAddress = new Uri("https://untrusted.example.test") });
        Assert.Equal(HttpStatusCode.BadRequest, (await hostile.GetAsync("/api/status")).StatusCode);
    }
    [Fact]
    public async Task Protected_keys_are_encrypted_in_postgres_and_survive_new_host()
    {
        var settings = Settings();
        string protectedValue;
        await using (var first = Factory(settings))
        {
            protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-check").Protect("only-test-data");
        }
        await using (var db = postgres.OpenDbContext())
        {
            var xml = await db.DataProtectionKeys.Select(x => x.Xml).ToListAsync();
            Assert.NotEmpty(xml); Assert.All(xml, value => Assert.Contains("EncryptedData", value));
        }
        await using var second = Factory(settings);
        Assert.Equal("only-test-data", second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-check").Unprotect(protectedValue));
    }
    [Fact]
    public async Task Neon_connection_without_certificate_verification_is_rejected_before_network()
    {
        var settings = Settings();
        settings["ConnectionStrings:Rcc"] = "Host=ep-fixture.neon.tech;Database=fixture;Username=fixture;SSL Mode=Require";
        await using var factory = Factory(settings);
        Assert.Contains("VerifyFull", Assert.Throws<InvalidOperationException>(() => factory.CreateClient()).Message);
    }
    [Fact]
    public async Task Missing_certificate_cannot_start_production()
    {
        var settings = Settings(); settings["DataProtection:CertificateBase64"] = "";
        await using var factory = Factory(settings);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
    [Fact]
    public async Task Render_proxy_mode_cannot_be_enabled_without_managed_environment()
    {
        var settings = Settings(); settings["Deployment:RenderProxy"] = "true"; settings["RENDER"] = "false";
        await using var factory = Factory(settings);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
    [Fact]
    public async Task Trusted_render_ingress_enables_secure_csrf_over_internal_http()
    {
        var settings = Settings(); settings["Deployment:RenderProxy"] = "true"; settings["RENDER"] = "true"; settings["RENDER_EXTERNAL_HOSTNAME"] = "rcc.example.test";
        await using var factory = Factory(settings);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://rcc.example.test") });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.25");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "untrusted.example.test");
        var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secure", Assert.Single(response.Headers.GetValues("Set-Cookie")));
    }
}
