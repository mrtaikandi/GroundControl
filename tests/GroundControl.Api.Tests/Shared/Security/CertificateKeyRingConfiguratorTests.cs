using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GroundControl.Api.Shared.Security.Certificate;
using GroundControl.Api.Shared.Security.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class CertificateKeyRingConfiguratorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-keys-{Guid.NewGuid():N}");
    private readonly List<X509Certificate2> _certificates = [];

    [Fact]
    public void Configure_PersistsKeysToConfiguredDirectory_AndProtectsWithCertificate()
    {
        // Arrange
        var certificate = CreateSelfSignedCertificate();
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificateAsync(Arg.Any<CancellationToken>())
            .Returns(certificate);
        provider.GetPreviousCertificatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<X509Certificate2>>([]));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyStorePath"] = _tempDir
            })
            .Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new CertificateKeyRingConfigurator(provider);

        // Act
        configurator.Configure(dpBuilder, configuration);
        services.AddSingleton<IValueProtector, DataProtectionValueProtector>();

        var serviceProvider = services.BuildServiceProvider();
        var protector = serviceProvider.GetRequiredService<IValueProtector>();

        // Force key generation by protecting a value
        var protectedValue = protector.Protect("test-value");

        // Assert
        Directory.Exists(_tempDir).ShouldBeTrue();
        Directory.GetFiles(_tempDir, "*.xml").ShouldNotBeEmpty();
        protectedValue.ShouldNotBe("test-value");
    }

    [Fact]
    public void Configure_ThrowsInvalidOperationException_WhenKeyStorePathNotConfigured()
    {
        // Arrange
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new CertificateKeyRingConfigurator(provider);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => configurator.Configure(dpBuilder, configuration));

        exception.Message.ShouldContain("KeyStorePath");
    }

    [Fact]
    public void Configure_RegistersPreviousCertificatesForDecryption()
    {
        // Arrange
        var currentCert = CreateSelfSignedCertificate();
        var previousCert = CreateSelfSignedCertificate();

        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        provider.GetCurrentCertificateAsync(Arg.Any<CancellationToken>())
            .Returns(currentCert);
        provider.GetPreviousCertificatesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<X509Certificate2>>([previousCert]));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyStorePath"] = _tempDir
            })
            .Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new CertificateKeyRingConfigurator(provider);

        // Act & Assert — should not throw
        Should.NotThrow(() => configurator.Configure(dpBuilder, configuration));
        provider.Received(1).GetPreviousCertificatesAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        foreach (var cert in _certificates)
        {
            cert.Dispose();
        }

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
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