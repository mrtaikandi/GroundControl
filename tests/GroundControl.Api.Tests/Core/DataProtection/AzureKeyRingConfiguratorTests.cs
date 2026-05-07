using Azure.Identity;
using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using DataProtectionOptions = GroundControl.Api.Core.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Tests.Core.DataProtection;

public sealed class AzureKeyRingConfiguratorTests
{
    [Fact]
    public void Configure_OptionsValidationException_WhenBlobUriNotConfigured()
    {
        // Arrange
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure
        };

        var services = new ServiceCollection();
        var builder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new AzureKeyRingConfigurator(new DefaultAzureCredential());

        // Act & Assert
        var exception = Should.Throw<OptionsValidationException>(() => configurator.Configure(builder, options));

        exception.Message.ShouldContain("BlobUri: The AzureOptions.BlobUri field is required.");
    }

    [Fact]
    public void Configure_OptionsValidationException_WhenKeyVaultKeyIdNotConfigured()
    {
        // Arrange
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure,
            Azure = new AzureOptions
            {
                BlobUri = new Uri("https://test.blob.core.windows.net/keys/key.xml")
            }
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new AzureKeyRingConfigurator(new DefaultAzureCredential());

        // Act & Assert
        var exception = Should.Throw<OptionsValidationException>(() => configurator.Configure(dpBuilder, options));

        exception.Message.ShouldContain("KeyVaultKeyId: The AzureOptions.KeyVaultKeyId field is required.");
    }

    [Fact]
    public void Configure_HappyPath_WiresUpAzureBlobRepositoryAndAzureKeyVaultEncryptor()
    {
        // Arrange — End-to-end Azure mode requires real Azure (no Key Vault emulator), so this
        // test verifies registration only: after Configure runs, KeyManagementOptions points at
        // the Azure-flavoured XmlRepository and XmlEncryptor types. No network call is made
        // because the underlying clients are constructed lazily.
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Azure,
            Azure = new AzureOptions
            {
                BlobUri = new Uri("https://account.blob.core.windows.net/keys/key.xml"),
                KeyVaultKeyId = new Uri("https://kv.vault.azure.net/keys/dp/abc")
            }
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection().SetApplicationName("GroundControl.Tests");
        var configurator = new AzureKeyRingConfigurator(new DefaultAzureCredential());

        // Act
        configurator.Configure(dpBuilder, options);

        var keyManagementOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        // Assert — Type names assert the integration with the Azure DataProtection packages
        // without taking a hard reference to internal types in those packages.
        keyManagementOptions.XmlRepository.ShouldNotBeNull();
        keyManagementOptions.XmlRepository.GetType().Name.ShouldContain("AzureBlob");

        keyManagementOptions.XmlEncryptor.ShouldNotBeNull();
        keyManagementOptions.XmlEncryptor.GetType().Name.ShouldContain("KeyVault");
    }
}