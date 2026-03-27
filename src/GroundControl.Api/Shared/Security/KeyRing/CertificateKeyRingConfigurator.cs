using GroundControl.Api.Shared.Security.Certificate;
using Microsoft.AspNetCore.DataProtection;

namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Persists Data Protection keys to the file system and protects them with an X.509 certificate.
/// </summary>
internal sealed class CertificateKeyRingConfigurator(IDataProtectionCertificateProvider certificateProvider) : IKeyRingConfigurator
{
    /// <inheritdoc />
    public void Configure(IDataProtectionBuilder builder, IConfiguration configuration)
    {
        var keyStorePath = configuration["DataProtection:KeyStorePath"]
            ?? throw new InvalidOperationException("DataProtection:KeyStorePath is required for Certificate mode.");

        var keyDirectory = new DirectoryInfo(keyStorePath);

        // Sync-over-async: safe here because this runs in the startup path with no SynchronizationContext.
        var certificate = certificateProvider.GetCurrentCertificateAsync()
            .GetAwaiter().GetResult();

        builder
            .PersistKeysToFileSystem(keyDirectory)
            .ProtectKeysWithCertificate(certificate);

        foreach (var previous in certificateProvider.GetPreviousCertificatesAsync().GetAwaiter().GetResult())
        {
            builder.UnprotectKeysWithAnyCertificate(previous);
        }
    }
}