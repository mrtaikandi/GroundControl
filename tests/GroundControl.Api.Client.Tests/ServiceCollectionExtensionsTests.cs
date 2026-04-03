using GroundControl.Api.Client.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Api.Client.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGroundControlClient_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddGroundControlClient(client => client.BaseAddress = new Uri("https://localhost")));
    }

    [Fact]
    public void AddGroundControlClient_NullConfigureClient_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddGroundControlClient(null!));
    }

    [Fact]
    public void AddGroundControlClient_ReturnsIHttpClientBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGroundControlClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void AddGroundControlClient_RegistersClientAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var instance1 = provider.GetRequiredService<IGroundControlClient>();
        var instance2 = provider.GetRequiredService<IGroundControlClient>();

        // Assert
        instance1.ShouldNotBeSameAs(instance2);
    }

    [Fact]
    public void AddGroundControlClient_ConfiguresHttpClientBaseAddress()
    {
        // Arrange
        var expectedBaseAddress = new Uri("https://api.groundcontrol.example.com");
        var services = new ServiceCollection();
        services.AddGroundControlClient(client => client.BaseAddress = expectedBaseAddress);

        using var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        var httpClient = httpClientFactory.CreateClient(typeof(IGroundControlClient).Name);

        // Assert
        httpClient.BaseAddress.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void AddGroundControlClient_ResolvesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGroundControlClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        using var provider = services.BuildServiceProvider();

        // Act
        var client = provider.GetRequiredService<IGroundControlClient>();

        // Assert
        client.ShouldNotBeNull();
        client.ShouldBeOfType<GroundControlClient>();
    }

    [Fact]
    public void AddGroundControlClient_ReturnsBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddGroundControlClient(
            client => client.BaseAddress = new Uri("https://localhost"));

        // Assert
        builder.ShouldNotBeNull();
    }
}