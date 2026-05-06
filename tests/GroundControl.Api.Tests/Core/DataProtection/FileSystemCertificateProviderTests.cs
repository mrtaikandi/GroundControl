using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GroundControl.Api.Core.DataProtection.Certificate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var provider = new FileSystemCertificateProvider(BuildOptions(pfxPath, password: null), _logger);

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
        var provider = new FileSystemCertificateProvider(BuildOptions(pfxPath, password), _logger);

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
        var provider = new FileSystemCertificateProvider(BuildOptions("/nonexistent/path/cert.pfx", password: null), _logger);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void GetCurrentCertificate_ThrowsCryptographicException_WhenPasswordIsWrong()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: "correct-password");
        var provider = new FileSystemCertificateProvider(BuildOptions(pfxPath, password: "wrong-password"), _logger);

        // Act & Assert
        Should.Throw<CryptographicException>(() => provider.GetCurrentCertificate());
    }

    [Fact]
    public void Validator_FailsValidation_WhenPathIsEmpty()
    {
        // Arrange
        var options = new FileSystemCertificateOptions();
        var validator = new FileSystemCertificateOptions.Validator();

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(FileSystemCertificateOptions.Path));
    }

    [Fact]
    public void GetPreviousCertificates_ReturnsEmptyList_WhenNotConfigured()
    {
        // Arrange
        var pfxPath = CreateTestCertificate(password: null);
        var provider = new FileSystemCertificateProvider(BuildOptions(pfxPath, password: null), _logger);

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
        var options = BuildOptions(currentPath, password: null, previousPaths: [firstPreviousPath, secondPreviousPath]);
        var provider = new FileSystemCertificateProvider(options, _logger);

        // Act
        var result = provider.GetPreviousCertificates();

        // Assert
        result.Count.ShouldBe(2);
        result[0].HasPrivateKey.ShouldBeTrue();
        result[1].HasPrivateKey.ShouldBeTrue();
    }

    private static IOptions<FileSystemCertificateOptions> BuildOptions(string path, string? password, IReadOnlyList<string>? previousPaths = null) =>
        Options.Create(new FileSystemCertificateOptions
        {
            Path = path,
            Password = password,
            PreviousPaths = previousPaths is null ? [] : [.. previousPaths]
        });

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