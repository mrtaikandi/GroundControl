using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Configures Data Protection key encryption using X.509 certificates resolved from DI.
/// </summary>
/// <remarks>
/// Defers certificate loading from service registration time to the first resolution of
/// <see cref="KeyManagementOptions"/>. The certificate provider is resolved from DI so the
/// configured logger is used instead of <c>NullLoggerFactory</c>.
/// </remarks>
internal sealed class CertificateKeyEncryptionConfigurator : IConfigureOptions<KeyManagementOptions>
{
    private readonly IDataProtectionCertificateProvider _certificateProvider;
    private readonly ILoggerFactory _loggerFactory;

    public CertificateKeyEncryptionConfigurator(IDataProtectionCertificateProvider certificateProvider, ILoggerFactory loggerFactory)
    {
        _certificateProvider = certificateProvider;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public void Configure(KeyManagementOptions options)
    {
        var certificate = _certificateProvider.GetCurrentCertificate();
        options.XmlEncryptor = new CertificateXmlEncryptor(certificate, _loggerFactory);
    }
}
