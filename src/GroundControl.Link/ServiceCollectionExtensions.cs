using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using GroundControl.Link.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Link;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register GroundControl background services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "GroundControl";

    /// <summary>
    /// Registers GroundControl background services: connection strategy, health check, and metrics.
    /// Requires configuration to have been set up via <see cref="ConfigurationBuilderExtensions.AddGroundControl"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The built configuration root containing the GroundControl provider.</param>
    /// <param name="configure">An optional delegate to customize <see cref="GroundControlServiceOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="GroundControlConfigurationProvider"/> is found in the configuration.
    /// Call <see cref="ConfigurationBuilderExtensions.AddGroundControl"/> first.
    /// </exception>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Singletons are owned by the DI container and disposed at shutdown")]
    public static IServiceCollection AddGroundControl(
        this IServiceCollection services,
        IConfigurationRoot configuration,
        Action<GroundControlServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = FindProvider(configuration);
        var store = provider.Store;
        var cache = provider.Cache;
        var options = new GroundControlServiceOptions();
        configure?.Invoke(options);

        // Core singletons extracted from the configuration provider
        services.AddSingleton(store);
        services.AddSingleton(cache);

        // Metrics
        services.AddMetrics();
        services.AddSingleton<GroundControlMetrics>();

        // Health check
        services.AddHealthChecks()
            .AddCheck<GroundControlHealthCheck>("GroundControl", tags: options.HealthCheckTags);

        if (store.Options.ConnectionMode == ConnectionMode.StartupOnly)
        {
            return services;
        }

        // Named HttpClient for background services
        var httpBuilder = services.AddHttpClient(HttpClientName, httpClient =>
            {
                httpClient.BaseAddress = new Uri(store.Options.ServerUrl);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(HeaderNames.ApiKey, $"{store.Options.ClientId}:{store.Options.ClientSecret}");
                httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, store.Options.ApiVersion);
            })
            .UseSocketsHttpHandler((handler, _) =>
            {
                handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        options.ConfigureHttpClient?.Invoke(httpBuilder);

        // ISseClient: NoOp for Polling mode, DefaultSseClient otherwise
        services.AddSingleton<ISseClient>(sp =>
        {
            if (store.Options.ConnectionMode == ConnectionMode.Polling)
            {
                return NoOpSseClient.Instance;
            }

            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultSseClient>();
            return new DefaultSseClient(httpClient, store.Options, logger);
        });

        // IConfigFetcher
        services.AddSingleton<IConfigFetcher>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultConfigFetcher>();
            return new DefaultConfigFetcher(httpClient, logger);
        });

        // IConnectionStrategy
        services.AddSingleton<IConnectionStrategy>(sp => store.Options.ConnectionMode switch
        {
            ConnectionMode.Polling => ActivatorUtilities.CreateInstance<PollingConnectionStrategy>(sp),
            ConnectionMode.Sse => ActivatorUtilities.CreateInstance<SseConnectionStrategy>(sp),
            ConnectionMode.SseWithPollingFallback => ActivatorUtilities.CreateInstance<SseWithPollingFallbackStrategy>(sp),
            _ => throw new InvalidOperationException(
                $"Unsupported connection mode: {store.Options.ConnectionMode}")
        });

        // Background service
        services.AddHostedService<GroundControlBackgroundService>();

        return services;
    }

    private static GroundControlConfigurationProvider FindProvider(IConfigurationRoot configuration)
    {
        foreach (var provider in configuration.Providers)
        {
            if (provider is GroundControlConfigurationProvider gcProvider)
            {
                return gcProvider;
            }
        }

        throw new InvalidOperationException(
            "No GroundControlConfigurationProvider found in configuration. " +
            "Call IConfigurationBuilder.AddGroundControl() before calling IServiceCollection.AddGroundControl().");
    }
}