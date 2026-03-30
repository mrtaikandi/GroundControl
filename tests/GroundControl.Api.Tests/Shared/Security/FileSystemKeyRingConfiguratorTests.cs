using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataProtectionOptions = GroundControl.Api.Core.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class FileSystemKeyRingConfiguratorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"gc-keys-{Guid.NewGuid():N}");

    [Fact]
    public void Configure_PersistsKeysToConfiguredDirectory()
    {
        // Arrange
        var options = new DataProtectionOptions
        {
            KeyStorePath = _tempDir
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act
        configurator.Configure(dpBuilder, options);
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
        var options = new DataProtectionOptions();
        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act & Assert — should not throw
        Should.NotThrow(() => configurator.Configure(dpBuilder, options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}