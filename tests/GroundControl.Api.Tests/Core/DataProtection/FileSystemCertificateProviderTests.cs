using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

public sealed class FileSystemCertificateProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-cert-{Guid.NewGuid():N}");
    private readonly ILogger<FileSystemCertificateProvider> _logger = NullLogger<FileSystemCertificateProvider>.Instance;

    public FileSystemCertificateProviderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetCurrentCertificate_LoadsValidPfxWithoutPassword()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: null);
        var configuration = BuildConfiguration(pfxPath, password: null);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var certificate = provider.GetCurrentCertificate();

        // Assert
        certificate.ShouldNotBeNull();
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.Thumbprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetCurrentCertificate_LoadsValidPfxWithPassword()
    {
        // Arrange
        var password = "test-password-123";
        var pfxPath = CreateTestCertificate(password);
        var configuration = BuildConfiguration(pfxPath, password);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var certificate = provider.GetCurrentCertificate();

        // Assert
        certificate.ShouldNotBeNull();
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.Thumbprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetCurrentCertificate_ThrowsFileNotFoundException_WhenPathDoesNotExist()
    {
        // Arrange
        var configuration = BuildConfiguration("/nonexistent/path/cert.pfx", password: null);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void GetCurrentCertificate_ThrowsCryptographicException_WhenPasswordIsWrong()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: "correct-password");
        var configuration = BuildConfiguration(pfxPath, password: "wrong-password");
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        Should.Throw<CryptographicException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void GetCurrentCertificate_ThrowsInvalidOperationException_WhenPathNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => provider.GetCurrentCertificate());
        exception.Message.ShouldContain("DataProtection:CertificatePath");
    }

    [Fact]
    public void GetPreviousCertificates_ReturnsEmptyList_WhenNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var result = provider.GetPreviousCertificates();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetPreviousCertificates_LoadsConfiguredPaths()
    {
        // Arrange
        var currentPath = CreateTestCertificate(password: null);
        var firstPreviousPath = CreateTestCertificate(password: null);
        var secondPreviousPath = CreateTestCertificate(password: null);
        var configuration = BuildConfiguration(currentPath, password: null, previousPaths: [firstPreviousPath, secondPreviousPath]);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var result = provider.GetPreviousCertificates();

        // Assert
        result.Count.ShouldBe(2);
        result[0].HasPrivateKey.ShouldBeTrue();
        result[1].HasPrivateKey.ShouldBeTrue();
    }

    private static IConfiguration BuildConfiguration(string path, string? password, IReadOnlyList<string>? previousPaths = null)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["DataProtection:CertificatePath"] = path
        };

        if (password is not null)
        {
            configValues["DataProtection:CertificatePassword"] = password;
        }

        if (previousPaths is not null)
        {
            for (var i = 0; i < previousPaths.Count; i++)
            {
                configValues[$"DataProtection:PreviousCertificatePaths:{i}"] = previousPaths[i];
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    private string CreateTestCertificate(string? password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=GroundControl Test Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var pfxPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.pfx");
        var pfxBytes = certificate.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(pfxPath, pfxBytes);

        return pfxPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}