using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GroundControl.Api.Tests.Core.DataProtection.Lifecycle;

/// <summary>
/// Generates self-signed X.509 certificates for Data Protection lifecycle tests.
/// </summary>
internal static class SelfSignedCertificate
{
    private static X509Certificate2 Create(string subjectName = "CN=GroundControl Test", DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddYears(1));
    }

    public static string CreatePfxFile(string directory, string fileName, string? password = null)
    {
        Directory.CreateDirectory(directory);
        using var certificate = Create();

        var path = Path.Combine(directory, fileName);
        var pfxBytes = certificate.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfxBytes);

        return path;
    }
}