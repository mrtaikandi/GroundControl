using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.Certificate;
using GroundControl.Api.Shared.Security.Protection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies <see cref="DataProtectionModule"/> registers the right services for each
/// <see cref="DataProtectionMode"/> / <see cref="CertificateProviderMode"/> combination, and
/// surfaces validation and unknown-enum failures fast.
/// </summary>
public sealed class DataProtectionModuleTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-module-{Guid.NewGuid():N}");

    public DataProtectionModuleTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void OnServiceConfiguration_FileSystem_DoesNotRegisterCertificateOrAzureServices()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.FileSystem,
            KeyStorePath = _tempDir
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IValueProtector)
            && d.ImplementationType == typeof(DataProtectionValueProtector));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(TokenCredential));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IDataProtectionCertificateProvider));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(GroundControlCertificateXmlEncryptor));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)
            && d.ImplementationType == typeof(CertificateKeyEncryptionConfigurator));
    }

    [Fact]
    public void OnServiceConfiguration_Certificate_RegistersCertificateProviderAndEncryptorPipeline()
    {
        // Arrange
        var pfxPath = CreateSelfSignedPfx(password: null);
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            KeyStorePath = _tempDir,
            CertificateProvider = CertificateProviderMode.FileSystem,
            FileSystemCertificate = new FileSystemCertificateOptions { Path = pfxPath }
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IDataProtectionCertificateProvider)
            && d.ImplementationType == typeof(FileSystemCertificateProvider));
        builder.Services.ShouldContain(d => d.ServiceType == typeof(GroundControlCertificateXmlEncryptor));
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IConfigureOptions<KeyManagementOptions>)
            && d.ImplementationType == typeof(CertificateKeyEncryptionConfigurator));
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IValueProtector)
            && d.ImplementationType == typeof(DataProtectionValueProtector));
    }

    [Fact]
    public void OnServiceConfiguration_AzureBlobCertificateProvider_RegistersTokenCredentialAndAzureBlobProvider()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.Certificate,
            KeyStorePath = _tempDir,
            CertificateProvider = CertificateProviderMode.AzureBlob,
            AzureBlobCertificate = new AzureBlobCertificateOptions
            {
                BlobUri = new Uri("https://account.blob.core.windows.net/certs/dp.pfx")
            },
            AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.Default }
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert
        builder.Services.Count(d => d.ServiceType == typeof(TokenCredential)).ShouldBe(1);
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IDataProtectionCertificateProvider)
            && d.ImplementationType == typeof(AzureBlobCertificateProvider));
    }

    [Fact]
    public void OnServiceConfiguration_AzureMode_RegistersTokenCredentialAndNoCertificateProvider()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure,
            KeyStorePath = _tempDir,
            Azure = new AzureOptions
            {
                BlobUri = new Uri("https://account.blob.core.windows.net/keys/key.xml"),
                KeyVaultKeyId = new Uri("https://kv.vault.azure.net/keys/dp/abc")
            },
            AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.Default }
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert
        builder.Services.ShouldContain(d => d.ServiceType == typeof(TokenCredential));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IDataProtectionCertificateProvider));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(GroundControlCertificateXmlEncryptor));
    }

    [Fact]
    public void OnServiceConfiguration_AzureMode_AndAzureBlobCertificateProvider_SharesSingleTokenCredential()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure,
            KeyStorePath = _tempDir,
            Azure = new AzureOptions
            {
                BlobUri = new Uri("https://account.blob.core.windows.net/keys/key.xml"),
                KeyVaultKeyId = new Uri("https://kv.vault.azure.net/keys/dp/abc")
            },
            CertificateProvider = CertificateProviderMode.AzureBlob,
            AzureBlobCertificate = new AzureBlobCertificateOptions
            {
                BlobUri = new Uri("https://account.blob.core.windows.net/certs/dp.pfx")
            },
            AzureCredential = new AzureCredentialOptions { Mode = AzureCredentialType.Default }
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert — both consumers (Azure key ring + AzureBlob certificate provider) share the
        // single registered TokenCredential singleton.
        builder.Services.Count(d => d.ServiceType == typeof(TokenCredential)).ShouldBe(1);
    }

    [Fact]
    public void OnServiceConfiguration_AlwaysRegistersIValueProtector()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.FileSystem,
            KeyStorePath = _tempDir
        });

        // Act
        module.OnServiceConfiguration(builder);

        // Assert
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IValueProtector)
            && d.ImplementationType == typeof(DataProtectionValueProtector));
    }

    [Fact]
    public void OnServiceConfiguration_ThrowsOptionsValidationException_WhenOptionsInvalid()
    {
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = DataProtectionMode.FileSystem,
            KeyStorePath = string.Empty
        });

        // Act & Assert — fails fast before any DI registration.
        Should.Throw<OptionsValidationException>(() => module.OnServiceConfiguration(builder));
    }

    [Fact]
    public void OnServiceConfiguration_ThrowsInvalidOperationException_ForUnknownMode()
    {
        // Arrange — Cast to invalid enum value to exercise the configurator factory's default arm.
        // We must bypass the validator to reach the factory; the validator only catches *known*
        // invalid combinations.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        var module = new DataProtectionModule(new DataProtectionOptions
        {
            Mode = (DataProtectionMode)999,
            KeyStorePath = _tempDir
        });

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => module.OnServiceConfiguration(builder));
        ex.Message.ShouldContain(nameof(DataProtectionOptions.Mode));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Disk locks (AV scans, lingering file handles) should not fail an otherwise green test.
            }
        }
    }

    private string CreateSelfSignedPfx(string? password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=GroundControl Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(1));

        var pfxBytes = certificate.Export(X509ContentType.Pfx, password);
        var pfxPath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(pfxPath, pfxBytes);
        return pfxPath;
    }
}
