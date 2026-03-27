using Microsoft.Extensions.Logging.Abstractions;

namespace GroundControl.Link;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates a <see cref="GroundControlConfigurationProvider"/>
/// for loading configuration from a GroundControl server.
/// </summary>
public sealed class GroundControlConfigurationSource : IConfigurationSource
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
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var logger = (_options.LoggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<GroundControlConfigurationProvider>();

        // Concrete implementations will be provided by T043 (DefaultSseClient),
        // T044 (DefaultConfigFetcher), and T045 (FileConfigCache).
        ISseClient sseClient = NoOpSseClient.Instance;
        IConfigFetcher configFetcher = NoOpConfigFetcher.Instance;
        IConfigCache configCache = NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(_options, sseClient, configFetcher, configCache, logger);
    }

    private sealed class NoOpSseClient : ISseClient
    {
        public static NoOpSseClient Instance { get; } = new();

        public async IAsyncEnumerable<SseEvent> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoOpConfigFetcher : IConfigFetcher
    {
        public static NoOpConfigFetcher Instance { get; } = new();

        public Task<FetchResult?> FetchAsync(string? etag, CancellationToken cancellationToken = default) =>
            Task.FromResult<FetchResult?>(null);
    }
}