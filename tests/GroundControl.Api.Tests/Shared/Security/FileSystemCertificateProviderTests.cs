using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GroundControl.Api.Shared.Security.Certificate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class FileSystemCertificateProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-cert-{Guid.NewGuid():N}");
    private readonly ILogger<FileSystemCertificateProvider> _logger = NullLogger<FileSystemCertificateProvider>.Instance;

    public FileSystemCertificateProviderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task GetCurrentCertificateAsync_LoadsValidPfxWithoutPassword()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: null);
        var configuration = BuildConfiguration(pfxPath, password: null);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var certificate = await provider.GetCurrentCertificateAsync(TestContext.Current.CancellationToken);

        // Assert
        certificate.ShouldNotBeNull();
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.Thumbprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCurrentCertificateAsync_LoadsValidPfxWithPassword()
    {
        // Arrange
        var password = "test-password-123";
        var pfxPath = CreateTestCertificate(password);
        var configuration = BuildConfiguration(pfxPath, password);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var certificate = await provider.GetCurrentCertificateAsync(TestContext.Current.CancellationToken);

        // Assert
        certificate.ShouldNotBeNull();
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.Thumbprint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCurrentCertificateAsync_ThrowsFileNotFoundException_WhenPathDoesNotExist()
    {
        // Arrange
        var configuration = BuildConfiguration("/nonexistent/path/cert.pfx", password: null);
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(
            () => provider.GetCurrentCertificateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCurrentCertificateAsync_ThrowsCryptographicException_WhenPasswordIsWrong()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: "correct-password");
        var configuration = BuildConfiguration(pfxPath, password: "wrong-password");
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        await Should.ThrowAsync<CryptographicException>(
            () => provider.GetCurrentCertificateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCurrentCertificateAsync_ThrowsInvalidOperationException_WhenPathNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetCurrentCertificateAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("DataProtection:CertificatePath");
    }

    [Fact]
    public async Task GetPreviousCertificatesAsync_ReturnsEmptyList()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new FileSystemCertificateProvider(configuration, _logger);

        // Act
        var result = await provider.GetPreviousCertificatesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    private static IConfiguration BuildConfiguration(string path, string? password)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["DataProtection:CertificatePath"] = path
        };

        if (password is not null)
        {
            configValues["DataProtection:CertificatePassword"] = password;
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