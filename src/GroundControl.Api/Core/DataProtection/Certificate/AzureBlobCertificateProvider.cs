using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Downloads X.509 certificates from Azure Blob Storage using an injected
/// <see cref="TokenCredential"/>.
/// </summary>
/// <remarks>
/// Uses the Azure SDK's synchronous <c>BlobClient.DownloadContent</c> API rather than
/// blocking on the async overload. The provider is invoked once at host startup, where the
/// blocking download is acceptable, and avoiding sync-over-async eliminates any deadlock risk
/// regardless of synchronization context.
/// </remarks>
internal sealed partial class AzureBlobCertificateProvider : IDataProtectionCertificateProvider
{
    private readonly AzureBlobCertificateOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<AzureBlobCertificateProvider> _logger;
    private readonly Func<Uri, TokenCredential, BlobClient> _blobClientFactory;

    public AzureBlobCertificateProvider(IOptions<AzureBlobCertificateOptions> options, TokenCredential credential, ILogger<AzureBlobCertificateProvider> logger)
        : this(options, credential, logger, static (uri, cred) => new BlobClient(uri, cred))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobCertificateProvider"/> class for testing, allowing injection of a custom BlobClient factory.
    /// </summary>
    internal AzureBlobCertificateProvider(
        IOptions<AzureBlobCertificateOptions> options,
        TokenCredential credential,
        ILogger<AzureBlobCertificateProvider> logger,
        Func<Uri, TokenCredential, BlobClient> blobClientFactory)
    {
        _options = options.Value;
        _credential = credential;
        _logger = logger;
        _blobClientFactory = blobClientFactory;
    }

    /// <inheritdoc />
    public X509Certificate2 GetCurrentCertificate() => DownloadCertificate(_options.BlobUri!, "AzureBlob");

    /// <inheritdoc />
    public IReadOnlyList<X509Certificate2> GetPreviousCertificates()
    {
        var uris = _options.PreviousBlobUris;
        if (uris.Length == 0)
        {
            return [];
        }

        var certificates = new List<X509Certificate2>(uris.Length);
        certificates.AddRange(uris.Select(uri => DownloadCertificate(uri, "AzureBlob (previous)")));

        return certificates;
    }

    private X509Certificate2 DownloadCertificate(Uri blobUri, string source)
    {
        var client = _blobClientFactory(blobUri, _credential);
        var response = client.DownloadContent();
        var pfxBytes = response.Value.Content.ToArray();

        var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, _options.Password, X509KeyStorageFlags.EphemeralKeySet);
        LogCertificateLoaded(_logger, source, certificate.Thumbprint);

        return certificate;
    }

    [LoggerMessage(1, LogLevel.Information, "Loaded certificate from {Source} with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateLoaded(ILogger logger, string source, string thumbprint);
}