using System.Security.Cryptography.X509Certificates;

namespace GroundControl.Api.Shared.Security.Certificate;

/// <summary>
/// Loads X.509 certificates from the local file system.
/// </summary>
internal sealed partial class FileSystemCertificateProvider(
    IConfiguration configuration,
    ILogger<FileSystemCertificateProvider> logger) : IDataProtectionCertificateProvider
{
    /// <inheritdoc />
    public Task<X509Certificate2> GetCurrentCertificateAsync(CancellationToken cancellationToken = default)
    {
        var path = configuration["DataProtection:CertificatePath"]
            ?? throw new InvalidOperationException("DataProtection:CertificatePath is required.");

        var password = configuration["DataProtection:CertificatePassword"];

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Certificate not found at path: {path}");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.EphemeralKeySet);

        LogCertificateLoaded(logger, "FileSystem", certificate.Thumbprint);

        return Task.FromResult(certificate);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<X509Certificate2>> GetPreviousCertificatesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<X509Certificate2>>([]);

    [LoggerMessage(1, LogLevel.Information, "Loaded certificate from {Source} with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateLoaded(ILogger logger, string source, string thumbprint);
}