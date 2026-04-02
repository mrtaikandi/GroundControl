using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GroundControl.Api.Client.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGroundControlApiClient_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddGroundControlApiClient(client => client.BaseAddress = new Uri("https://localhost")));
    }

    [Fact]
    public void AddGroundControlApiClient_NullConfigureClient_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddGroundControlApiClient(null!));
    }

    [Fact]
    public void AddGroundControlApiClient_ReturnsIHttpClientBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControlApiClient_RegistersClientAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var instance1 = provider.GetRequiredService<GroundControlApiClient>();
        var instance2 = provider.GetRequiredService<GroundControlApiClient>();

        // Assert
        instance1.ShouldNotBeSameAs(instance2);
    }

    [Fact]
    public void AddGroundControlApiClient_ConfiguresHttpClientBaseAddress()
    {
        // Arrange
        var expectedBaseAddress = new Uri("https://api.groundcontrol.example.com");
        var services = new ServiceCollection();
        services.AddGroundControlApiClient(client => client.BaseAddress = expectedBaseAddress);

        using var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        var httpClient = httpClientFactory.CreateClient(nameof(GroundControlApiClient));

        // Assert
        httpClient.BaseAddress.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void AddGroundControlApiClient_WithCustomAuth_ResolvesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"),
            _ => new ApiKeyAuthenticationProvider(Guid.NewGuid(), "test-secret"));

        using var provider = services.BuildServiceProvider();

        // Act
        var client = provider.GetRequiredService<GroundControlApiClient>();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControlApiClient_WithoutAuth_UsesAnonymousFallback()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var client = provider.GetRequiredService<GroundControlApiClient>();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControlApiClient_WithDIRegisteredAuth_ResolvesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationProvider>(
            new ApiKeyAuthenticationProvider(Guid.NewGuid(), "test-secret"));
        services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var client = provider.GetRequiredService<GroundControlApiClient>();

        // Assert
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControlApiClient_ReturnsBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        // Assert
        builder.ShouldNotBeNull();
        builder.Name.ShouldBe(nameof(GroundControlApiClient));
    }
}