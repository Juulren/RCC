using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
namespace Rcc.Api.Deployment;

public static class DeploymentSecrets
{
    public static void Prepare()
    {
        // Only newly generated deployment material, outside source control. No provider credentials are read.
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RCC", "deployment");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "cookie-certificate.env");
        if (File.Exists(path)) throw new InvalidOperationException("El certificado ya existe; conservelo para no invalidar sesiones. No se sobrescribe.");
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=RCC cookie key protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var encoded = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password));
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(path, options))
        using (var writer = new StreamWriter(stream))
            writer.Write("DataProtection__CertificateBase64=" + encoded + "\nDataProtection__CertificatePassword=" + password + "\n");
        Console.WriteLine("Configuracion privada generada en el perfil del usuario: " + path);
        Console.WriteLine("Importe ese archivo SOLO en las variables privadas del backend. No compartir ni commitear. Conserve copia privada.");
    }
}
