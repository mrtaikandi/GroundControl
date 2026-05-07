using GroundControl.Api.Core.DataProtection;
using GroundControl.Api.Core.DataProtection.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using DataProtectionOptions = GroundControl.Api.Core.DataProtection.DataProtectionOptions;

namespace GroundControl.Api.Tests.Core.DataProtection;

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

    [Fact]
    public void Configure_WrapsRedisConnectionFailure_InInvalidOperationException()
    {
        // Arrange — Point at a TCP port that nothing should be listening on, with a tiny timeout
        // so the test fails fast and surfaces the wrapping exception path rather than hanging.
        const string UnreachableEndpoint = "127.0.0.1:1";
        var options = new DataProtectionOptions
        {
            Mode = DataProtectionMode.Redis,
            Redis = new RedisOptions
            {
                ConnectionString = UnreachableEndpoint,
                ConnectTimeoutMs = 200
            }
        };

        var services = new ServiceCollection();
        var builder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new RedisKeyRingConfigurator();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => configurator.Configure(builder, options));

        // Assert — The wrapper preserves the inner exception and surfaces the connection string
        // so operators can spot misconfiguration in the logs.
        exception.Message.ShouldContain(UnreachableEndpoint);
        exception.InnerException.ShouldNotBeNull();
    }
}