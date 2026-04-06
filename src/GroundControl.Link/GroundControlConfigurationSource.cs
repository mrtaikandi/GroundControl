using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using GroundControl.Link.Internals;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroundControl.Link;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates a <see cref="GroundControlConfigurationProvider"/>
/// for loading configuration from a GroundControl server.
/// </summary>
internal sealed class GroundControlConfigurationSource : IConfigurationSource
{
    private readonly GroundControlOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroundControlConfigurationSource"/> class.
    /// </summary>
    /// <param name="options">The validated SDK options.</param>
    public GroundControlConfigurationSource(GroundControlOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient is owned by the provider")]
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var loggerFactory = _options.LoggerFactory ?? NullLoggerFactory.Instance;

        var httpClient = new HttpClient { BaseAddress = new Uri(_options.ServerUrl) };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{_options.ClientId}:{_options.ClientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, _options.ApiVersion);

        ISseClient sseClient = _options.ConnectionMode == ConnectionMode.Polling
            ? NoOpSseClient.Instance
            : new DefaultSseClient(httpClient, _options, loggerFactory.CreateLogger<DefaultSseClient>());

        IConfigFetcher configFetcher = new DefaultConfigFetcher(
            httpClient, loggerFactory.CreateLogger<DefaultConfigFetcher>());

        IConfigCache configCache = _options.EnableLocalCache
            ? new FileConfigCache(_options, loggerFactory.CreateLogger<FileConfigCache>())
            : NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(
            httpClient,
            _options,
            sseClient,
            configFetcher,
            configCache,
            loggerFactory.CreateLogger<GroundControlConfigurationProvider>());
    }
}