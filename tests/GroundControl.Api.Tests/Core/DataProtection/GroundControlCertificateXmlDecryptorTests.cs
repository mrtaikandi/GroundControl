using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Edge-case coverage for <see cref="GroundControlCertificateXmlDecryptor"/> beyond the happy
/// paths covered by <see cref="GroundControlCertificateXmlRoundTripTests"/>.
/// </summary>
public sealed class GroundControlCertificateXmlDecryptorTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void ServiceProviderConstructor_NullServices_Throws()
    {
        // Arrange + Act + Assert
        Should.Throw<ArgumentNullException>(() => new GroundControlCertificateXmlDecryptor(services: null!));
    }

    [Fact]
    public void ServiceProviderConstructor_ResolvesProviderAndLoggerFromContainer()
    {
        // Arrange — A real DI container with the two dependencies registered. This mirrors the
        // path ASP.NET Data Protection's SimpleActivator takes when it constructs the decryptor.
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        var services = new ServiceCollection()
            .AddSingleton(provider)
            .AddLogging()
            .BuildServiceProvider();

        // Act
        var decryptor = new GroundControlCertificateXmlDecryptor(services);

        // Assert — Construction itself proves both dependencies were resolved.
        decryptor.ShouldNotBeNull();
    }

    [Fact]
    public void Decrypt_NullEncryptedElement_Throws()
    {
        // Arrange
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        var decryptor = new GroundControlCertificateXmlDecryptor(provider, NullLogger<GroundControlCertificateXmlDecryptor>.Instance);

        // Act + Assert
        Should.Throw<ArgumentNullException>(() => decryptor.Decrypt(encryptedElement: null!));
    }

    [Fact]
    public void Decrypt_WhenMatchingCertificateLacksPrivateKey_FailsWithCryptographicException()
    {
        // Arrange — Encrypt under a full (private+public) cert. Then build a provider that
        // returns the same cert but with the private key stripped. FindCertificateWithPrivateKey
        // returns null in that case, so decryption falls through to the base EncryptedXml which
        // can't find the cert in the OS store either.
        var fullCertificate = CreateSelfSignedCertificate();
        var encryptionProvider = Substitute.For<IDataProtectionCertificateProvider>();
        encryptionProvider.GetCurrentCertificate().Returns(fullCertificate);

        var encryptor = new GroundControlCertificateXmlEncryptor(encryptionProvider, NullLoggerFactory.Instance);
        var encrypted = encryptor.Encrypt(new XElement("payload", "no-private-key-on-decrypt"));

        var publicOnly = X509CertificateLoader.LoadCertificate(fullCertificate.Export(X509ContentType.Cert));
        _certificates.Add(publicOnly);
        var publicOnlyProvider = Substitute.For<IDataProtectionCertificateProvider>();
        publicOnlyProvider.GetCurrentCertificate().Returns(publicOnly);
        publicOnlyProvider.GetPreviousCertificates().Returns([]);

        var decryptor = new GroundControlCertificateXmlDecryptor(
            publicOnlyProvider,
            NullLogger<GroundControlCertificateXmlDecryptor>.Instance);

        // Act + Assert
        Should.Throw<CryptographicException>(() => decryptor.Decrypt(encrypted.EncryptedElement));
    }

    public void Dispose()
    {
        foreach (var cert in _certificates)
        {
            cert.Dispose();
        }
    }

    private X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=GroundControl Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1));

        _certificates.Add(certificate);
        return certificate;
    }
}
