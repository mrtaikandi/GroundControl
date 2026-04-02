using GroundControl.Api.Client.Contracts;
using Microsoft.Extensions.DependencyInjection;

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
        var instance1 = provider.GetRequiredService<IGroundControlApiClient>();
        var instance2 = provider.GetRequiredService<IGroundControlApiClient>();

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
        var httpClient = httpClientFactory.CreateClient(typeof(IGroundControlApiClient).Name);

        // Assert
        httpClient.BaseAddress.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void AddGroundControlApiClient_ResolvesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlApiClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var client = provider.GetRequiredService<IGroundControlApiClient>();

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeOfType<GroundControlClient>();
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
    }
}