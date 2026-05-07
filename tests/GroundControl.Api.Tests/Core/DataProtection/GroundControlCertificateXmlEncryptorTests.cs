using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies that <see cref="GroundControlCertificateXmlEncryptor"/> caches the inner
/// <c>CertificateXmlEncryptor</c> per certificate thumbprint, rebuilds it after rotation,
/// and tolerates concurrent <see cref="GroundControlCertificateXmlEncryptor.Encrypt"/> calls.
/// </summary>
public sealed class GroundControlCertificateXmlEncryptorTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Encrypt_TwiceWithSameCertificate_ReusesInnerEncryptor()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certificate);
        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);

        // Act
        encryptor.Encrypt(new XElement("first"));
        var innerAfterFirst = ReadInnerEncryptor(encryptor);
        encryptor.Encrypt(new XElement("second"));
        var innerAfterSecond = ReadInnerEncryptor(encryptor);

        // Assert — same certificate => exact same inner instance reused, no re-allocation.
        innerAfterFirst.ShouldNotBeNull();
        innerAfterSecond.ShouldBeSameAs(innerAfterFirst);
    }

    [Fact]
    public void Encrypt_AfterCertificateRotation_RebuildsInnerEncryptor()
    {
        // Arrange — Provider returns cert A first, then cert B (simulating a rotation).
        var certA = CreateSelfSignedCertificate();
        var certB = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certA, certB);

        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);

        // Act
        encryptor.Encrypt(new XElement("under-a"));
        var innerForA = ReadInnerEncryptor(encryptor);
        encryptor.Encrypt(new XElement("under-b"));
        var innerForB = ReadInnerEncryptor(encryptor);

        // Assert — different thumbprint => a fresh inner encryptor.
        innerForA.ShouldNotBeNull();
        innerForB.ShouldNotBeNull();
        innerForB.ShouldNotBeSameAs(innerForA);
    }

    [Fact]
    public async Task Encrypt_UnderConcurrency_AllCallsProduceDecryptableOutput()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certificate);
        provider.GetPreviousCertificates().Returns([]);

        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);
        var decryptor = new GroundControlCertificateXmlDecryptor(provider, NullLogger<GroundControlCertificateXmlDecryptor>.Instance);

        var results = new ConcurrentBag<EncryptedXmlInfo>();

        // Act — 32 concurrent encrypts; the cache field is unsynchronised, so this pins the
        // current "benign race" behaviour: parallel callers may briefly construct extra inner
        // encryptors but every result is still valid and decryptable.
        await Parallel.ForAsync(0, 32, TestContext.Current.CancellationToken, (i, _) =>
        {
            results.Add(encryptor.Encrypt(new XElement("payload", new XAttribute("i", i))));
            return ValueTask.CompletedTask;
        });

        // Assert
        results.Count.ShouldBe(32);
        foreach (var encrypted in results)
        {
            var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);
            decrypted.Name.LocalName.ShouldBe("payload");
        }
    }

    public void Dispose()
    {
        foreach (var cert in _certificates)
        {
            cert.Dispose();
        }
    }

    /// <summary>
    /// Reads the inner <c>CertificateXmlEncryptor</c> reference from the encryptor's private
    /// cache field via reflection so the tests can verify caching identity without changing the
    /// production surface.
    /// </summary>
    private static object? ReadInnerEncryptor(GroundControlCertificateXmlEncryptor encryptor)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cacheField = typeof(GroundControlCertificateXmlEncryptor).GetField("_cache", Flags);
        cacheField.ShouldNotBeNull("the encryptor must have an inner cache field for these tests to be meaningful");

        var cache = cacheField.GetValue(encryptor);
        if (cache is null)
        {
            return null;
        }

        var encryptorProperty = cache.GetType().GetProperty("Encryptor", Flags | BindingFlags.Public);
        return encryptorProperty?.GetValue(cache);
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
