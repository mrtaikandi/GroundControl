using GroundControl.Api.Shared.Security.Certificate;
using GroundControl.Api.Shared.Security.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

public sealed class RedisKeyRingConfiguratorTests
{
    [Fact]
    public void Configure_ThrowsInvalidOperationException_WhenConnectionStringNotConfigured()
    {
        // Arrange
        var provider = Substitute.For<IDataProtectionCertificateProvider>();
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("GroundControl.Tests");

        var configurator = new RedisKeyRingConfigurator(provider);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => configurator.Configure(dpBuilder, configuration));

        exception.Message.ShouldContain("ConnectionString");
    }
}