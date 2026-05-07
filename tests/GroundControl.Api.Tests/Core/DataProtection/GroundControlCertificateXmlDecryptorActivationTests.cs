using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.AspNetCore.DataProtection.Internal;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Pins the contract that <see cref="GroundControlCertificateXmlDecryptor"/> can be constructed
/// through ASP.NET Data Protection's <see cref="IActivator"/> — the path the framework actually
/// takes when it sees the <c>GroundControlCertificateXmlEncryptor</c>'s pinned decryptor type
/// in persisted key XML. Every other test bypasses this via the internal test-only constructor;
/// without this test, breaking the public <c>IServiceProvider</c> ctor would only fail in
/// production.
/// </summary>
public sealed class GroundControlCertificateXmlDecryptorActivationTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Activator_BuildsDecryptor_AndDecryptsAPayloadProducedByTheEncryptor()
    {
        // Arrange — a real DI container with the same shape DataProtectionModule sets up at
        // startup, then the framework's IActivator constructs our decryptor via the public
        // IServiceProvider ctor.
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificate().Returns(certificate);
        provider.GetPreviousCertificates().Returns([]);

        var encryptor = new GroundControlCertificateXmlEncryptor(provider, NullLoggerFactory.Instance);
        var encrypted = encryptor.Encrypt(new XElement("payload", "via-activator"));

        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddSingleton(provider);
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        var activator = serviceProvider.GetRequiredService<IActivator>();

        // Act
        var decryptor = (IXmlDecryptor)activator.CreateInstance(
            typeof(IXmlDecryptor),
            typeof(GroundControlCertificateXmlDecryptor).AssemblyQualifiedName!);
        var decrypted = decryptor.Decrypt(encrypted.EncryptedElement);

        // Assert
        decrypted.Value.ShouldBe("via-activator");
    }

    [Fact]
    public void DirectActivator_CreateInstance_WithPublicServiceProviderConstructor()
    {
        // Arrange — Belt-and-braces: confirm System.Activator can instantiate the decryptor
        // through the public single-IServiceProvider ctor. SimpleActivator falls back to this
        // when no IActivator-compatible service is registered.
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(provider)
            .AddLogging()
            .BuildServiceProvider();

        // Act
        var instance = Activator.CreateInstance(typeof(GroundControlCertificateXmlDecryptor), serviceProvider);

        // Assert
        instance.ShouldBeOfType<GroundControlCertificateXmlDecryptor>();
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
