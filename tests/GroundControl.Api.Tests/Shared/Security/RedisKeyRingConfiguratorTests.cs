using GroundControl.Api.Shared.Security.DataProtection;
using GroundControl.Api.Shared.Security.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using DataProtectionOptions = GroundControl.Api.Shared.Security.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class RedisKeyRingConfiguratorTests
{
    [Fact]
    public void Configure_OptionsValidationException_WhenConnectionStringNotConfigured()
    {
        // Arrange
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Redis,
            Redis = new RedisOptions()
        };

        var services = new ServiceCollection();
        var builder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new RedisKeyRingConfigurator();

        // Act & Assert
        var exception = Should.Throw<OptionsValidationException>(() => configurator.Configure(builder, options));

        exception.Message.ShouldContain("ConnectionString: The RedisOptions.ConnectionString field is required.");
    }
}