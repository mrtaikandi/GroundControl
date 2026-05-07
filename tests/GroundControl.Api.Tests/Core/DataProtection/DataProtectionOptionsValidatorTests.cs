using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.Certificate;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies <see cref="DataProtectionOptions.Validator"/> enforces mode/provider consistency
/// and dispatches to the right sub-options validators for the active mode.
/// </summary>
public sealed class DataProtectionOptionsValidatorTests
{
    [Fact]
    public void Validate_ReturnsFail_WhenKeyStorePathIsEmpty()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = ValidFileSystemOptions();
        options.KeyStorePath = string.Empty;

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.KeyStorePath)));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Validate_ReturnsFail_WhenKeyStorePathIsWhitespace(string keyStorePath)
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = ValidFileSystemOptions();
        options.KeyStorePath = keyStorePath;

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.KeyStorePath)));
    }

    [Fact]
    public void Validate_ReturnsFail_WhenCertificateProviderMissingForCertificateMode()
        => AssertCertificateProviderRequired(DataProtectionMode.Certificate);

    [Fact]
    public void Validate_ReturnsFail_WhenCertificateProviderMissingForRedisMode()
        => AssertCertificateProviderRequired(DataProtectionMode.Redis);

    [Fact]
    public void Validate_DoesNotRequireCertificateProvider_ForFileSystemMode()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();

        // Act
        var result = validator.Validate(name: null, ValidFileSystemOptions());

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_DoesNotRequireCertificateProvider_ForAzureMode()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();

        // Act
        var result = validator.Validate(name: null, ValidAzureOptions());

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    private static void AssertCertificateProviderRequired(DataProtectionMode mode)
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = mode,
            CertificateProvider = null,
            Redis = new RedisOptions { ConnectionString = "localhost:6379" }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.CertificateProvider)));
    }

    [Fact]
    public void Validate_DispatchesToFileSystemCertificateValidator_WhenCertificateProviderIsFileSystem()
    {
        // Arrange — Mode=Certificate + Provider=FileSystem with empty Path should surface
        // the FileSystemCertificateOptions failure prefixed with the property name.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            CertificateProvider = CertificateProviderMode.FileSystem,
            FileSystemCertificate = new FileSystemCertificateOptions { Path = string.Empty }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.FileSystemCertificate))
            && failure.Contains(nameof(FileSystemCertificateOptions.Path)));
    }

    [Fact]
    public void Validate_DispatchesToAzureBlobCertificateValidator_WhenCertificateProviderIsAzureBlob()
    {
        // Arrange — Provider=AzureBlob without BlobUri should surface a failure pointing at AzureBlobCertificate.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            CertificateProvider = CertificateProviderMode.AzureBlob,
            AzureBlobCertificate = new AzureBlobCertificateOptions { BlobUri = null }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.AzureBlobCertificate))
            && failure.Contains(nameof(AzureBlobCertificateOptions.BlobUri)));
    }

    [Fact]
    public void Validate_DispatchesToAzureCredentialValidator_WhenModeIsAzure()
    {
        // Arrange — Mode=Azure + ClientSecret credential mode missing required fields should surface failures.
        var validator = new DataProtectionOptions.Validator();
        var options = ValidAzureOptions();
        options.AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.ClientSecret };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.AzureCredential))
            && failure.Contains(nameof(AzureCredentialOptions.TenantId)));
    }

    [Fact]
    public void Validate_DispatchesToAzureCredentialValidator_WhenCertificateProviderIsAzureBlob()
    {
        // Arrange — Even with Mode=Certificate, AzureBlob provider triggers credential validation.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            CertificateProvider = CertificateProviderMode.AzureBlob,
            AzureBlobCertificate = new AzureBlobCertificateOptions { BlobUri = new Uri("https://account.blob.core.windows.net/certs/dp.pfx") },
            AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.ClientSecret }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.AzureCredential)));
    }

    [Fact]
    public void Validate_DispatchesToRedisValidator_WhenModeIsRedis()
    {
        // Arrange — Mode=Redis + empty ConnectionString surfaces a failure pointing at Redis.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Redis,
            CertificateProvider = CertificateProviderMode.FileSystem,
            FileSystemCertificate = new FileSystemCertificateOptions { Path = "/certs/dp.pfx" },
            Redis = new RedisOptions { ConnectionString = string.Empty }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.Redis))
            && failure.Contains(nameof(RedisOptions.ConnectionString)));
    }

    [Fact]
    public void Validate_DispatchesToAzureValidator_WhenModeIsAzure()
    {
        // Arrange — Mode=Azure with missing Azure.BlobUri should surface a failure prefixed with Azure.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure,
            Azure = new AzureOptions { BlobUri = null, KeyVaultKeyId = new Uri("https://kv.vault.azure.net/keys/k/v") }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(DataProtectionOptions.Azure))
            && failure.Contains(nameof(AzureOptions.BlobUri)));
    }

    [Fact]
    public void Validate_AggregatesMultipleFailures()
    {
        // Arrange — Empty KeyStorePath AND missing CertificateProvider AND empty Redis connection string.
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Redis,
            CertificateProvider = null,
            KeyStorePath = string.Empty,
            Redis = new RedisOptions { ConnectionString = string.Empty }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldNotBeNull();
        result.Failures.Count().ShouldBeGreaterThanOrEqualTo(2);
        result.Failures.ShouldContain(f => f.Contains(nameof(DataProtectionOptions.KeyStorePath)));
        result.Failures.ShouldContain(f => f.Contains(nameof(DataProtectionOptions.CertificateProvider)));
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidFileSystemConfiguration()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = ValidFileSystemOptions();

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidCertificateConfiguration()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            CertificateProvider = CertificateProviderMode.FileSystem,
            FileSystemCertificate = new FileSystemCertificateOptions { Path = "/certs/dp.pfx" }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidRedisConfiguration()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Redis,
            CertificateProvider = CertificateProviderMode.FileSystem,
            FileSystemCertificate = new FileSystemCertificateOptions { Path = "/certs/dp.pfx" },
            Redis = new RedisOptions { ConnectionString = "localhost:6379" }
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidAzureConfiguration()
    {
        // Arrange
        var validator = new DataProtectionOptions.Validator();
        var options = ValidAzureOptions();

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    private static DataProtectionOptions ValidFileSystemOptions() => new()
    {
        Mode = DataProtectionMode.FileSystem,
        KeyStorePath = "./keys"
    };

    private static DataProtectionOptions ValidAzureOptions() => new()
    {
        Mode = DataProtectionMode.Azure,
        Azure = new AzureOptions
        {
            BlobUri = new Uri("https://account.blob.core.windows.net/keys/key.xml"),
            KeyVaultKeyId = new Uri("https://kv.vault.azure.net/keys/dp/abc")
        },
        AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.Default }
    };
}
