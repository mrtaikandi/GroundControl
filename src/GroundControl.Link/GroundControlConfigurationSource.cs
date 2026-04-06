using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handler is owned and disposed by HttpClient; HttpClient is owned by the provider")]
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var loggerFactory = _options.LoggerFactory ?? NullLoggerFactory.Instance;

        var handler = new GroundControlAuthHandler(_options) { InnerHandler = new HttpClientHandler() };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(_options.ServerUrl) };

        ISseClient sseClient = _options.ConnectionMode == ConnectionMode.Polling
            ? NoOpSseClient.Instance
            : new DefaultSseClient(httpClient, _options, loggerFactory.CreateLogger<DefaultSseClient>());

        IConfigFetcher configFetcher = new DefaultConfigFetcher(
            httpClient, _options, loggerFactory.CreateLogger<DefaultConfigFetcher>());

        IConfigCache configCache = _options.EnableLocalCache
            ? new FileConfigCache(_options, loggerFactory.CreateLogger<FileConfigCache>())
            : NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(
            _options, sseClient, configFetcher, configCache,
            loggerFactory.CreateLogger<GroundControlConfigurationProvider>(),
            httpClient);
    }

    internal sealed class NoOpSseClient : ISseClient
    {
        public static NoOpSseClient Instance { get; } = new();

        public async IAsyncEnumerable<SseEvent> StreamAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal sealed class NoOpConfigFetcher : IConfigFetcher
    {
        public static NoOpConfigFetcher Instance { get; } = new();

        public Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FetchResult { Status = FetchStatus.TransientError });
    }
}