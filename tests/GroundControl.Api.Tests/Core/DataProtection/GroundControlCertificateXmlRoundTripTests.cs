using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies that <see cref="GroundControlCertificateXmlEncryptor"/> and
/// <see cref="GroundControlCertificateXmlDecryptor"/> round-trip XML payloads, including across
/// certificate rotation where the cert that protected a payload has moved into the previous list.
/// </summary>
public sealed class GroundControlCertificateXmlRoundTripTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Encrypt_PinsTheGroundControlDecryptorTypeIntoTheKeyMetadata()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certificate);

        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);
        var plaintext = new XElement("secret", "round-trip-payload");

        // Act
        var encrypted = encryptor.Encrypt(plaintext);

        // Assert
        encrypted.DecryptorType.ShouldBe(typeof(GroundControlCertificateXmlDecryptor));
        encrypted.EncryptedElement.ShouldNotBeNull();
    }

    [Fact]
    public void EncryptThenDecrypt_RestoresOriginalElement_WithCurrentCertificate()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certificate);
        provider.GetPreviousCertificates().Returns([]);

        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);
        var decryptor = new GroundControlCertificateXmlDecryptor(provider, NullLogger<GroundControlCertificateXmlDecryptor>.Instance);
        var plaintext = new XElement("secret", new XAttribute("kind", "value"), "before-rotation");

        // Act
        var encrypted = encryptor.Encrypt(plaintext);
        var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);

        // Assert
        XNode.DeepEquals(decrypted, plaintext).ShouldBeTrue("decrypted element should be structurally equal to the original");
    }

    [Fact]
    public void DecryptUnderRotatedKey_FindsCertificateInPreviousList()
    {
        // Arrange — Encrypt under c1 while it is current.
        var c1 = CreateSelfSignedCertificate();
        var encryptionProvider = Substitute.For<IDataProtectionCertificateProvider>();
        encryptionProvider.GetCurrentCertificate().Returns(c1);

        var encryptor = new GroundControlCertificateXmlEncryptor(encryptionProvider, NullLoggerFactory.Instance);
        var plaintext = new XElement("secret", "encrypted-under-c1");
        var encrypted = encryptor.Encrypt(plaintext);

        // Act — Now c2 is current and c1 has been moved to previous; decrypt the c1 payload.
        var c2 = CreateSelfSignedCertificate();
        var rotatedProvider = Substitute.For<IDataProtectionCertificateProvider>();
        rotatedProvider.GetCurrentCertificate().Returns(c2);
        rotatedProvider.GetPreviousCertificates().Returns([c1]);

        var decryptor = new GroundControlCertificateXmlDecryptor(rotatedProvider, NullLogger<GroundControlCertificateXmlDecryptor>.Instance);
        var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);

        // Assert
        XNode.DeepEquals(decrypted, plaintext).ShouldBeTrue();
    }

    [Fact]
    public void Decrypt_WhenNoMatchingCertificate_ThrowsAndLogsWarning()
    {
        // Arrange — Encrypt under c1, then build a provider that knows nothing about c1.
        var c1 = CreateSelfSignedCertificate();
        var encryptionProvider = Substitute.For<IDataProtectionCertificateProvider>();
        encryptionProvider.GetCurrentCertificate().Returns(c1);

        var encryptor = new GroundControlCertificateXmlEncryptor(encryptionProvider, NullLoggerFactory.Instance);
        var encrypted = encryptor.Encrypt(new XElement("secret", "orphaned-payload"));

        var unrelated = CreateSelfSignedCertificate();
        var unrelatedProvider = Substitute.For<IDataProtectionCertificateProvider>();
        unrelatedProvider.GetCurrentCertificate().Returns(unrelated);
        unrelatedProvider.GetPreviousCertificates().Returns([]);

        var collector = new FakeLogCollector();
        var logger = new FakeLogger<GroundControlCertificateXmlDecryptor>(collector);
        var decryptor = new GroundControlCertificateXmlDecryptor(unrelatedProvider, logger);

        // Act & Assert — decryption falls through to base EncryptedXml which can't find the cert
        // in the OS store either, so a CryptographicException surfaces. Before the throw we should
        // have logged a warning naming the orphaned thumbprint.
        Should.Throw<CryptographicException>(() => decryptor.Decrypt(encrypted.EncryptedElement));

        var snapshot = collector.GetSnapshot();
        var warning = snapshot.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(c1.Thumbprint, Case.Insensitive);
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