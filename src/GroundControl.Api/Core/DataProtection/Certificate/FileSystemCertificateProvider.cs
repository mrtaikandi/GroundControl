using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Loads X.509 certificates from the local file system.
/// </summary>
internal sealed partial class FileSystemCertificateProvider : IDataProtectionCertificateProvider
{
    private readonly FileSystemCertificateOptions _options;
    private readonly ILogger<FileSystemCertificateProvider> _logger;

    public FileSystemCertificateProvider(IOptions<FileSystemCertificateOptions> options, ILogger<FileSystemCertificateProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public X509Certificate2 GetCurrentCertificate() => LoadCertificate(_options.Path, _options.Password, "FileSystem");

    /// <inheritdoc />
    public IReadOnlyList<X509Certificate2> GetPreviousCertificates()
    {
        var paths = _options.PreviousPaths;
        if (paths.Length == 0)
        {
            return [];
        }

        var password = _options.Password;
        var certificates = new List<X509Certificate2>(paths.Length);
        certificates.AddRange(paths.Select(path => LoadCertificate(path, password, "FileSystem (previous)")));

        return certificates;
    }

    private X509Certificate2 LoadCertificate(string path, string? password, string source)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Certificate not found at path: {path}");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        LogCertificateLoaded(_logger, source, certificate.Thumbprint);

        return certificate;
    }

    [LoggerMessage(1, LogLevel.Information, "Loaded certificate from {Source} with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateLoaded(ILogger logger, string source, string thumbprint);
}