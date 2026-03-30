using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.KeyRing;
using Microsoft.AspNetCore.DataProtection;
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

        var configurator = new AzureKeyRingConfigurator();

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

        var configurator = new AzureKeyRingConfigurator();

        // Act & Assert
        var exception = Should.Throw<OptionsValidationException>(() => configurator.Configure(dpBuilder, options));

        exception.Message.ShouldContain("KeyVaultKeyId: The AzureOptions.KeyVaultKeyId field is required.");
    }
}