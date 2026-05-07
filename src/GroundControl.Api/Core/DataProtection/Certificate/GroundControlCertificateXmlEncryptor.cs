using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Encrypts Data Protection key XML using the current X.509 certificate resolved through DI,
/// and pins <see cref="GroundControlCertificateXmlDecryptor"/> as the decryptor type so the
/// decryption side can resolve certificates lazily through DI as well.
/// </summary>
/// <remarks>
/// Delegates the actual cryptographic work to <see cref="CertificateXmlEncryptor"/> from the
/// framework. The only customisation is the <see cref="EncryptedXmlInfo.DecryptorType"/>:
/// the framework would normally pin <c>EncryptedXmlDecryptor</c>, which only consults the
/// internal <c>XmlKeyDecryptionOptions</c>. Pointing at our own decryptor lets us look up
/// certificates through <see cref="IDataProtectionCertificateProvider"/> at first decrypt,
/// removing the need for <c>UnprotectKeysWithAnyCertificate</c> and any eager loading at
/// service registration time.
/// </remarks>
internal sealed class GroundControlCertificateXmlEncryptor : IXmlEncryptor
{
    private readonly IDataProtectionCertificateProvider _provider;
    private readonly ILoggerFactory _loggerFactory;

    public GroundControlCertificateXmlEncryptor(IDataProtectionCertificateProvider provider, ILoggerFactory loggerFactory)
    {
        _provider = provider;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var certificate = _provider.GetCurrentCertificate();
        var inner = new CertificateXmlEncryptor(certificate, _loggerFactory);
        var produced = inner.Encrypt(plaintextElement);

        return new EncryptedXmlInfo(produced.EncryptedElement, typeof(GroundControlCertificateXmlDecryptor));
    }
}