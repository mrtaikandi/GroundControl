using System.Security.Cryptography.X509Certificates;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Loads X.509 certificates from the local file system.
/// </summary>
internal sealed partial class FileSystemCertificateProvider : IDataProtectionCertificateProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileSystemCertificateProvider> _logger;

    public FileSystemCertificateProvider(IConfiguration configuration, ILogger<FileSystemCertificateProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public X509Certificate2 GetCurrentCertificate()
    {
        var path = _configuration["DataProtection:CertificatePath"] ?? throw new InvalidOperationException("DataProtection:CertificatePath is required.");
        return LoadCertificate(path, _configuration["DataProtection:CertificatePassword"], "FileSystem");
    }

    /// <inheritdoc />
    public IReadOnlyList<X509Certificate2> GetPreviousCertificates()
    {
        var paths = _configuration.GetSection("DataProtection:PreviousCertificatePaths").Get<string[]>() ?? [];
        if (paths.Length == 0)
        {
            return [];
        }

        var password = _configuration["DataProtection:CertificatePassword"];
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