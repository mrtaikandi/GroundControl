using System.Net;
using GroundControl.Api.Client;
using GroundControl.Cli.Shared.ApiClient;
using GroundControl.Host.Cli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Cli.Tests.ApiClient;

public sealed class ApiClientModuleTests
{
    [Fact]
    public void ConfigureServices_WithValidServerUrl_RegistersApiClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["GroundControl:ServerUrl"] = "https://example.com"
        });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();

        // Assert
        var client = provider.GetService<GroundControlApiClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_WithValidServerUrl_SetsBaseAddress()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["GroundControl:ServerUrl"] = "https://my-server.example.com"
        });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(GroundControlApiClient));

        // Assert
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.ToString().ShouldBe("https://my-server.example.com/");
    }

    [Fact]
    public void ConfigureServices_WithMissingServerUrl_ThrowsOnClientCreation()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = CreateContext(new Dictionary<string, string?>());

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();

        // Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<GroundControlApiClient>());

        exception.Message.ShouldContain("GroundControl server URL is not configured");
        exception.Message.ShouldContain("GroundControl__ServerUrl");
    }

    [Fact]
    public void ConfigureServices_WithEmptyServerUrl_ThrowsOnClientCreation()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["GroundControl:ServerUrl"] = "   "
        });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();

        // Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<GroundControlApiClient>());

        exception.Message.ShouldContain("GroundControl server URL is not configured");
    }

    [Fact]
    public void ConfigureServices_BindsOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["GroundControl:ServerUrl"] = "https://configured.example.com"
        });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GroundControlClientOptions>>();

        // Assert
        options.Value.ServerUrl.ShouldBe("https://configured.example.com");
    }

    [Fact]
    public void ConfigureServices_LaterConfigSourceOverridesEarlier()
    {
        // Arrange — simulates env var override: the real EnvironmentVariablesConfigurationProvider
        // normalizes GroundControl__ServerUrl to GroundControl:ServerUrl, so a later config source
        // with the same normalized key takes precedence.
        var services = new ServiceCollection();
        var context = CreateContext(
            jsonConfig: new Dictionary<string, string?>
            {
                ["GroundControl:ServerUrl"] = "https://from-json.example.com"
            },
            envConfig: new Dictionary<string, string?>
            {
                ["GroundControl:ServerUrl"] = "https://from-env.example.com"
            });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(GroundControlApiClient));

        // Assert
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.ToString().ShouldBe("https://from-env.example.com/");
    }

    [Fact]
    public async Task ConfigureServices_AllowsDelegatingHandlerRegistration()
    {
        // Arrange
        using var handler = new TestDelegatingHandler();
        var services = new ServiceCollection();

        var context = CreateContext(new Dictionary<string, string?>
        {
            ["GroundControl:ServerUrl"] = "https://example.com"
        });

        var module = new ApiClientModule();

        // Act
        module.ConfigureServices(context, services);

        services.AddHttpClient(nameof(GroundControlApiClient))
            .AddHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(nameof(GroundControlApiClient));

        await httpClient.GetAsync("https://example.com/test", TestContext.Current.CancellationToken);

        // Assert
        handler.WasInvoked.ShouldBeTrue();
    }

    private static DependencyModuleContext CreateContext(
        Dictionary<string, string?> jsonConfig,
        Dictionary<string, string?>? envConfig = null)
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(jsonConfig);

        if (envConfig is not null)
        {
            configBuilder.AddInMemoryCollection(envConfig);
        }

        var configuration = configBuilder.Build();
        var environment = Substitute.For<IHostEnvironment>();

        return new DependencyModuleContext(environment, configuration);
    }

    private sealed class TestDelegatingHandler : DelegatingHandler
    {
        public bool WasInvoked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}