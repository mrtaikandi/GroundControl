using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Configures Data Protection key encryption to use <see cref="GroundControlCertificateXmlEncryptor"/>.
/// </summary>
internal sealed class CertificateKeyEncryptionConfigurator : IConfigureOptions<KeyManagementOptions>
{
    private readonly GroundControlCertificateXmlEncryptor _encryptor;

    public CertificateKeyEncryptionConfigurator(GroundControlCertificateXmlEncryptor encryptor)
    {
        _encryptor = encryptor;
    }

    /// <inheritdoc />
    public void Configure(KeyManagementOptions options) => options.XmlEncryptor = _encryptor;
}