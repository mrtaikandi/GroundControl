using GroundControl.Api.Shared.Security.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class AzureKeyRingConfiguratorTests
{
    [Fact]
    public void Configure_ThrowsInvalidOperationException_WhenBlobUriNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new AzureKeyRingConfigurator();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => configurator.Configure(dpBuilder, configuration));

        exception.Message.ShouldContain("BlobUri");
    }

    [Fact]
    public void Configure_ThrowsInvalidOperationException_WhenKeyVaultKeyIdNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Azure:BlobUri"] = "https://test.blob.core.windows.net/keys/key.xml"
            })
            .Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new AzureKeyRingConfigurator();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => configurator.Configure(dpBuilder, configuration));

        exception.Message.ShouldContain("KeyVaultKeyId");
    }
}