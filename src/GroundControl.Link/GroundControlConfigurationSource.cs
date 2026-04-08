using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GroundControl.Link;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates a <see cref="GroundControlConfigurationProvider"/>
/// for loading configuration from a GroundControl server.
/// </summary>
internal sealed class GroundControlConfigurationSource : IConfigurationSource
{
    private readonly GroundControlOptions _options;

    public GroundControlConfigurationSource(GroundControlOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient ownership: disposed after Load() via provider")]
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var store = new GroundControlStore(_options);

        IConfigurationCache cache = _options.EnableLocalCache
            ? new FileConfigurationCache(_options, NullLogger<FileConfigurationCache>.Instance)
            : NullConfigurationCache.Instance;

        // Short-lived HttpClient for the conditional GET in Load().
        // Background services use IHttpClientFactory for long-lived connections.
        var httpClient = new HttpClient { BaseAddress = _options.ServerUrl };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(HeaderNames.ApiKey, $"{_options.ClientId}:{_options.ClientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, _options.ApiVersion);

        IGroundControlApiClient client = new GroundControlApiClient(httpClient, NullLogger<GroundControlApiClient>.Instance);
        return new GroundControlConfigurationProvider(store, cache, client);
    }
}