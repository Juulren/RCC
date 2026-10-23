using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Rcc.Api.Data;
namespace Rcc.Api.Deployment;

public static class ProductionConfiguration
{
    public static void AddRccProduction(this WebApplicationBuilder builder)
    {
        var production = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");
        if (!production) return;
        var config = builder.Configuration;
        var origin = config["Deployment:PublicOrigin"] ?? config["RENDER_EXTERNAL_URL"] ?? "";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Configure Deployment:PublicOrigin como origen HTTPS publico.");
        var database = new Npgsql.NpgsqlConnectionStringBuilder(config.GetConnectionString("Rcc"));
        if (database.Host?.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase) == true && database.SslMode != Npgsql.SslMode.VerifyFull)
            throw new InvalidOperationException("Las conexiones Neon requieren SSL Mode=VerifyFull.");
        config["AllowedHosts"] = uri.Host;
        if (string.IsNullOrEmpty(config["Stripe:PublicOrigin"])) config["Stripe:PublicOrigin"] = uri.GetLeftPart(UriPartial.Authority);
        var encoded = config["DataProtection:CertificateBase64"];
        var password = config["DataProtection:CertificatePassword"];
        if (string.IsNullOrWhiteSpace(encoded) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Configure el certificado privado de DataProtection; consulte DESPLIEGUE.md.");
        X509Certificate2 certificate;
        try { certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(encoded), password, X509KeyStorageFlags.EphemeralKeySet); }
        catch (Exception error) when (error is FormatException or System.Security.Cryptography.CryptographicException) { throw new InvalidOperationException("Certificado DataProtection invalido. No comparta su valor."); }
        if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) throw new InvalidOperationException("Se requiere un certificado DataProtection vigente con clave privada.");
        builder.Services.AddDataProtection().SetApplicationName("RCC").PersistKeysToDbContext<RccDbContext>().ProtectKeysWithCertificate(certificate);
        // Only Render's isolated managed ingress may opt into dynamic proxy addresses.
        // Host comes from validated configuration; X-Forwarded-Host is never trusted.
        var renderProxy = config.GetValue<bool>("Deployment:RenderProxy");
        if (renderProxy && (config["RENDER"] != "true" || config["RENDER_EXTERNAL_HOSTNAME"] != uri.Host))
            throw new InvalidOperationException("RenderProxy requiere el hostname y entorno administrado de Render.");
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            if (renderProxy) { options.KnownIPNetworks.Clear(); options.KnownProxies.Clear(); }
        });
    }
}
