using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace GroundControl.Api.Shared.Security.Certificate;

/// <summary>
/// Downloads X.509 certificates from Azure Blob Storage using <see cref="DefaultAzureCredential"/>.
/// </summary>
internal sealed partial class AzureBlobCertificateProvider(
    IConfiguration configuration,
    ILogger<AzureBlobCertificateProvider> logger) : IDataProtectionCertificateProvider
{
    /// <inheritdoc />
    public async Task<X509Certificate2> GetCurrentCertificateAsync(CancellationToken cancellationToken = default)
    {
        var blobUrl = configuration["DataProtection:AzureBlobUrl"]
            ?? throw new InvalidOperationException("DataProtection:AzureBlobUrl is required.");

        var credential = new DefaultAzureCredential();
        var client = new BlobClient(new Uri(blobUrl), credential);
        var response = await client.DownloadContentAsync(cancellationToken);
        var pfxBytes = response.Value.Content.ToArray();

        var certificate = X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            null,
            X509KeyStorageFlags.EphemeralKeySet);

        LogCertificateLoaded(logger, "AzureBlob", certificate.Thumbprint);

        return certificate;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<X509Certificate2>> GetPreviousCertificatesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<X509Certificate2>>([]);

    [LoggerMessage(1, LogLevel.Information, "Loaded certificate from {Source} with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateLoaded(ILogger logger, string source, string thumbprint);
}