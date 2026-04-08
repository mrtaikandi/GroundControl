using System.Net.Http.Headers;
using GroundControl.Link.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// <param name="configureHttpClient">An optional delegate to customize the named "GroundControl" <see cref="HttpClient"/>.</param>
    /// <param name="configureOptions">
    /// An optional delegate to further configure <see cref="GroundControlOptions"/> after loading from configuration.
    /// Note that options are already applied when the configuration provider is created, so this is only for additional settings that don't affect the core provider services.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="GroundControlConfigurationProvider"/> is found in the configuration.
    /// Call <see cref="ConfigurationBuilderExtensions.AddGroundControl"/> first.
    /// </exception>
    public static IServiceCollection AddGroundControl(
        this IServiceCollection services,
        IConfigurationRoot configuration,
        Action<IHttpClientBuilder>? configureHttpClient = null,
        Action<GroundControlOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = FindProvider(configuration);
        services.AddSingleton(provider.Store);
        services.AddSingleton(provider.Cache);

        var options = provider.Store.Options;
        configureOptions?.Invoke(options);
        services.AddSingleton(Options.Create(options));

        services.AddMetrics();
        services.AddSingleton<GroundControlMetrics>();

        services.AddHealthChecks().AddCheck<GroundControlHealthCheck>("GroundControl", tags: options.HealthCheckTags);

        if (options.ConnectionMode == ConnectionMode.StartupOnly)
        {
            return services;
        }

        var httpBuilder = services.AddHttpClient(
                HttpClientName,
                httpClient =>
                {
                    httpClient.BaseAddress = options.ServerUrl;
                    httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, options.ApiVersion);
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue(HeaderNames.ApiKey, $"{options.ClientId}:{options.ClientSecret}");
                })
            .UseSocketsHttpHandler((handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2))
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        configureHttpClient?.Invoke(httpBuilder);

        services.AddSingleton<ISseConfigClient>(sp =>
        {
            var groundControlOptions = sp.GetRequiredService<IOptions<GroundControlOptions>>();
            if (groundControlOptions.Value.ConnectionMode == ConnectionMode.Polling)
            {
                return NoOpSseConfigClient.Instance;
            }

            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultSseConfigClient>();

            return new DefaultSseConfigClient(httpClient, groundControlOptions, logger);
        });

        services.AddSingleton<IRestConfigClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<DefaultRestConfigClient>();

            return new DefaultRestConfigClient(httpClient, logger);
        });

        // IConnectionStrategy
        services.AddSingleton<IConnectionStrategy>(sp => options.ConnectionMode switch
        {
            ConnectionMode.Polling => ActivatorUtilities.CreateInstance<PollingConnectionStrategy>(sp),
            ConnectionMode.Sse => ActivatorUtilities.CreateInstance<SseConnectionStrategy>(sp),
            ConnectionMode.SseWithPollingFallback => ActivatorUtilities.CreateInstance<SseWithPollingFallbackStrategy>(sp),
            ConnectionMode.StartupOnly => throw new InvalidOperationException($"Connection strategy should not be registered for {nameof(ConnectionMode.StartupOnly)} mode."),
            _ => throw new InvalidOperationException($"Unsupported connection mode: {options.ConnectionMode}")
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