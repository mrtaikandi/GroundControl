using GroundControl.Api.Shared.Security.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class FileSystemKeyRingConfiguratorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-keys-{Guid.NewGuid():N}");

    [Fact]
    public void Configure_PersistsKeysToConfiguredDirectory()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyStorePath"] = _tempDir
            })
            .Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act
        configurator.Configure(dpBuilder, configuration);
        services.AddSingleton<IValueProtector, DataProtectionValueProtector>();

        var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IValueProtector>();

        // Force key generation by protecting a value
        protector.Protect("trigger-key-creation");

        // Assert
        Directory.Exists(_tempDir).ShouldBeTrue();
        Directory.GetFiles(_tempDir, "*.xml").ShouldNotBeEmpty();
    }

    [Fact]
    public void Configure_UsesDefaultPath_WhenNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act & Assert — should not throw
        Should.NotThrow(() => configurator.Configure(dpBuilder, configuration));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}