using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.KeyRing;
using GroundControl.Api.Shared.Security.Protection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using DataProtectionOptions = GroundControl.Api.Core.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Tests.Core.DataProtection;

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

    [Fact]
    public void Configure_UseDpapiTrue_OnWindows_RegistersDpapiXmlEncryptor()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows-only.");

        // Arrange
        var options = new DataProtectionOptions
        {
            KeyStorePath = _tempDir,
            UseDpapi = true
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act
        configurator.Configure(dpBuilder, options);
        var keyManagementOptions = services.BuildServiceProvider().GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        // Assert — DPAPI wires up an XmlEncryptor (the framework's DpapiXmlEncryptor type is
        // internal, so we assert by name to avoid an internal type reference).
        keyManagementOptions.XmlEncryptor.ShouldNotBeNull("ProtectKeysWithDpapi should configure an XmlEncryptor");
        keyManagementOptions.XmlEncryptor.GetType().Name.ShouldContain("Dpapi");
    }

    [Fact]
    public void Configure_UseDpapiFalse_OnWindows_DoesNotRegisterAnyXmlEncryptor()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows-only.");

        // Arrange
        var options = new DataProtectionOptions
        {
            KeyStorePath = _tempDir,
            UseDpapi = false
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act
        configurator.Configure(dpBuilder, options);
        var keyManagementOptions = services.BuildServiceProvider().GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        // Assert — Without DPAPI no XmlEncryptor is configured (keys persist as plaintext XML;
        // appropriate only for development).
        keyManagementOptions.XmlEncryptor.ShouldBeNull();
    }

    [Fact]
    public void Configure_UseDpapiTrue_OnNonWindows_IsNoOp()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Verifies the non-Windows branch.");

        // Arrange
        var options = new DataProtectionOptions
        {
            KeyStorePath = _tempDir,
            UseDpapi = true
        };

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new FileSystemKeyRingConfigurator();

        // Act + Assert — On Linux/macOS the DPAPI request is silently ignored rather than
        // throwing PlatformNotSupportedException.
        Should.NotThrow(() => configurator.Configure(dpBuilder, options));

        var keyManagementOptions = services.BuildServiceProvider().GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        keyManagementOptions.XmlEncryptor.ShouldBeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}