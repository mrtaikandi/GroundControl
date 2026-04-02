using GroundControl.Api.Client.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Api.Client;

/// <summary>
/// Provides dependency injection registration helpers for the GroundControl API client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGroundControlClient"/> and <see cref="GroundControlClient"/> with the
    /// dependency injection container using <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">A delegate to configure the <see cref="HttpClient"/>. At minimum, set the <see cref="HttpClient.BaseAddress"/>.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration.</returns>
    public static IHttpClientBuilder AddGroundControlClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureClient);

        return services.AddHttpClient<IGroundControlClient, GroundControlClient>(configureClient);
    }
}