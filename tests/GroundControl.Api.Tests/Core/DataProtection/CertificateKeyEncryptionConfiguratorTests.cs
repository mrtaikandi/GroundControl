using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

public sealed class CertificateKeyEncryptionConfiguratorTests : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Configure_SetsXmlEncryptorOnKeyManagementOptions()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificateAsync(Arg.Any<CancellationToken>())
            .Returns(certificate);

        var configurator = new CertificateKeyEncryptionConfigurator(provider, NullLoggerFactory.Instance);
        var options = new KeyManagementOptions();

        // Act
        configurator.Configure(options);

        // Assert
        options.XmlEncryptor.ShouldNotBeNull();
    }

    [Fact]
    public void Configure_CallsGetCurrentCertificateAsync()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificateAsync(Arg.Any<CancellationToken>())
            .Returns(certificate);

        var configurator = new CertificateKeyEncryptionConfigurator(provider, NullLoggerFactory.Instance);
        var options = new KeyManagementOptions();

        // Act
        configurator.Configure(options);

        // Assert
        provider.Received(1).GetCurrentCertificateAsync(Arg.Any<CancellationToken>());
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