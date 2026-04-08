using GroundControl.Link.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private static IConfigurationRoot BuildConfigWithGroundControl(ConnectionMode connectionMode = ConnectionMode.Polling)
    {
        var builder = new ConfigurationBuilder();
        builder.AddGroundControl(opts =>
        {
            opts.ServerUrl = new Uri("http://localhost:9999");
            opts.ClientId = "test";
            opts.ClientSecret = "secret";
            opts.EnableLocalCache = false;
            opts.ConnectionMode = connectionMode;
        });

        return (IConfigurationRoot)builder.Build();
    }

    [Fact]
    public void AddGroundControl_RegistersRequiredServices()
    {
        // Arrange
        var config = BuildConfigWithGroundControl();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Act
        services.AddGroundControl(config);
        using var sp = services.BuildServiceProvider();

        // Assert
        sp.GetRequiredService<GroundControlStore>().ShouldNotBeNull();
        sp.GetRequiredService<IConfigCache>().ShouldNotBeNull();
        sp.GetRequiredService<GroundControlMetrics>().ShouldNotBeNull();
        sp.GetRequiredService<IConnectionStrategy>().ShouldNotBeNull();
        sp.GetRequiredService<ISseClient>().ShouldNotBeNull();
        sp.GetRequiredService<IConfigFetcher>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControl_NoProviderInConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            services.AddGroundControl(config));
    }

    [Fact]
    public void AddGroundControl_RegistersHealthCheck()
    {
        // Arrange
        var config = BuildConfigWithGroundControl();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Act
        services.AddGroundControl(config);
        using var sp = services.BuildServiceProvider();

        // Assert
        var healthCheckService = sp.GetRequiredService<HealthCheckService>();
        healthCheckService.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControl_WithConfigureHttpClient_InvokesDelegate()
    {
        // Arrange
        var config = BuildConfigWithGroundControl();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        var httpBuilderCustomized = false;

        // Act
        services.AddGroundControl(config, _ => httpBuilderCustomized = true);

        // Assert
        httpBuilderCustomized.ShouldBeTrue();
    }

    [Fact]
    public void AddGroundControl_StartupOnly_SkipsBackgroundServices()
    {
        // Arrange
        var config = BuildConfigWithGroundControl(ConnectionMode.StartupOnly);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Act
        services.AddGroundControl(config);
        using var sp = services.BuildServiceProvider();

        // Assert — core services registered
        sp.GetRequiredService<GroundControlStore>().ShouldNotBeNull();
        sp.GetRequiredService<GroundControlMetrics>().ShouldNotBeNull();

        // Assert — background services NOT registered
        sp.GetService<IConnectionStrategy>().ShouldBeNull();
        sp.GetService<ISseClient>().ShouldBeNull();
        sp.GetService<IConfigFetcher>().ShouldBeNull();
    }
}