using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace GroundControl.Api.Client;

/// <summary>
/// Provides dependency injection registration helpers for the GroundControl API client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GroundControlApiClient"/> with the dependency injection container
    /// using <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">A delegate to configure the <see cref="HttpClient"/>. At minimum, set the <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="configureAuth">
    /// An optional factory that creates an <see cref="IAuthenticationProvider"/>.
    /// When <see langword="null"/>, <see cref="AnonymousAuthenticationProvider"/> is used.
    /// </param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration.</returns>
    public static IHttpClientBuilder AddGroundControlApiClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        Func<IServiceProvider, IAuthenticationProvider>? configureAuth = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureClient);

        services.AddTransient(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(GroundControlApiClient));
            var authProvider = configureAuth?.Invoke(sp)
                               ?? sp.GetService<IAuthenticationProvider>()
                               ?? new AnonymousAuthenticationProvider();

            return new GroundControlApiClient(
                new HttpClientRequestAdapter(authProvider, httpClient: httpClient));
        });

        return services.AddHttpClient(nameof(GroundControlApiClient))
            .ConfigureHttpClient(configureClient);
    }
}