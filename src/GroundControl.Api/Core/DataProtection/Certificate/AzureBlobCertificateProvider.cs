using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Downloads X.509 certificates from Azure Blob Storage using <see cref="DefaultAzureCredential"/>.
/// </summary>
/// <remarks>
/// Uses the Azure SDK's synchronous <c>BlobClient.DownloadContent</c> API rather than
/// blocking on the async overload. The provider is invoked once at host startup, where the
/// blocking download is acceptable, and avoiding sync-over-async eliminates any deadlock risk
/// regardless of synchronization context.
/// </remarks>
internal sealed partial class AzureBlobCertificateProvider : IDataProtectionCertificateProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureBlobCertificateProvider> _logger;

    public AzureBlobCertificateProvider(IConfiguration configuration,
        ILogger<AzureBlobCertificateProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private static readonly DefaultAzureCredential Credential = new();

    /// <inheritdoc />
    public X509Certificate2 GetCurrentCertificate()
    {
        var blobUrl = _configuration["DataProtection:AzureBlobUrl"] ?? throw new InvalidOperationException("DataProtection:AzureBlobUrl is required.");
        return DownloadCertificate(blobUrl, "AzureBlob");
    }

    /// <inheritdoc />
    public IReadOnlyList<X509Certificate2> GetPreviousCertificates()
    {
        var urls = _configuration.GetSection("DataProtection:PreviousAzureBlobUrls").Get<string[]>() ?? [];
        if (urls.Length == 0)
        {
            return [];
        }

        var certificates = new List<X509Certificate2>(urls.Length);
        certificates.AddRange(urls.Select(url => DownloadCertificate(url, "AzureBlob (previous)")));

        return certificates;
    }

    private X509Certificate2 DownloadCertificate(string blobUrl, string source)
    {
        var password = _configuration["DataProtection:CertificatePassword"];

        var client = new BlobClient(new Uri(blobUrl), Credential);
        var response = client.DownloadContent();
        var pfxBytes = response.Value.Content.ToArray();

        var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, password, X509KeyStorageFlags.EphemeralKeySet);
        LogCertificateLoaded(_logger, source, certificate.Thumbprint);

        return certificate;
    }

    [LoggerMessage(1, LogLevel.Information, "Loaded certificate from {Source} with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateLoaded(ILogger logger, string source, string thumbprint);
}
