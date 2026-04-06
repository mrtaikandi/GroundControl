using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace GroundControl.Link;

/// <summary>
/// A configuration provider that loads and live-updates configuration from a GroundControl server
/// via SSE streaming and REST polling, with local file cache fallback.
/// </summary>
public sealed partial class GroundControlConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly GroundControlOptions _options;
    private readonly ISseClient _sseClient;
    private readonly IConfigFetcher _configFetcher;
    private readonly IConfigCache _configCache;
    private readonly ILogger<GroundControlConfigurationProvider> _logger;
    private readonly HttpClient? _ownedHttpClient;
    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _sseCts;
    private string? _lastSseEventId;
    private string? _currentRestETag;
    private Task? _backgroundTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroundControlConfigurationProvider"/> class.
    /// </summary>
    /// <param name="options">The SDK options.</param>
    /// <param name="sseClient">The SSE client for streaming events.</param>
    /// <param name="configFetcher">The REST fetcher for polling.</param>
    /// <param name="configCache">The local cache for offline fallback.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ownedHttpClient">An optional <see cref="HttpClient"/> owned by this provider for disposal.</param>
    public GroundControlConfigurationProvider(
        GroundControlOptions options,
        ISseClient sseClient,
        IConfigFetcher configFetcher,
        IConfigCache configCache,
        ILogger<GroundControlConfigurationProvider> logger,
        HttpClient? ownedHttpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sseClient = sseClient ?? throw new ArgumentNullException(nameof(sseClient));
        _configFetcher = configFetcher ?? throw new ArgumentNullException(nameof(configFetcher));
        _configCache = configCache ?? throw new ArgumentNullException(nameof(configCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownedHttpClient = ownedHttpClient;
    }

    /// <inheritdoc />
    public override void Load()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        LoadInitialConfigAsync(_cts.Token).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _cts.Cancel();
        _sseCts?.Cancel();

        _sseClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _ownedHttpClient?.Dispose();

        _sseCts?.Dispose();
        _cts.Dispose();
    }

    private async Task LoadInitialConfigAsync(CancellationToken cancellationToken)
    {
        // Overall startup timeout bounds the entire initial acquisition (SSE + REST)
        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(_options.StartupTimeout);

        try
        {
            if (_options.ConnectionMode != ConnectionMode.Polling &&
                await TryLoadFromSseAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // REST fetch bounded by the overall startup timeout
            if (await TryLoadFromFetcherAsync(startupCts.Token).ConfigureAwait(false))
            {
                StartPolling();
                return;
            }
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            LogStartupTimeout(_logger, _options.StartupTimeout);
        }

        await TryLoadFromCacheAsync(cancellationToken).ConfigureAwait(false);
        StartPolling();
    }

    private async Task<bool> TryLoadFromSseAsync(CancellationToken cancellationToken)
    {
        _sseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var firstConfigReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamTask = StreamSseEventsAsync(firstConfigReceived, _sseCts.Token);

        var timeoutTask = Task.Delay(_options.StartupTimeout, cancellationToken);
        var completedTask = await Task.WhenAny(firstConfigReceived.Task, timeoutTask).ConfigureAwait(false);

        if (completedTask == firstConfigReceived.Task &&
            await firstConfigReceived.Task.ConfigureAwait(false))
        {
            _backgroundTask = streamTask;
            return true;
        }

        // SSE failed or timed out during startup — cancel the SSE stream
        await _sseCts.CancelAsync().ConfigureAwait(false);
        _sseCts.Dispose();
        _sseCts = null;

        cancellationToken.ThrowIfCancellationRequested();
        LogSseStartupFailed(_logger);
        return false;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: SSE errors are logged and trigger fallback to polling")]
    private async Task StreamSseEventsAsync(
        TaskCompletionSource<bool> firstConfigReceived,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var sseEvent in _sseClient.StreamAsync(cancellationToken).ConfigureAwait(false))
            {
                if (sseEvent.EventType != "config")
                {
                    continue;
                }

                var config = ParseConfigData(sseEvent.Data);
                ApplyConfig(config);
                _lastSseEventId = sseEvent.Id;
                _currentRestETag = ParseSnapshotVersion(sseEvent.Data);

                if (!firstConfigReceived.Task.IsCompleted)
                {
                    LogSseConnected(_logger);
                    firstConfigReceived.TrySetResult(true);
                }
                else
                {
                    LogSseConfigUpdated(_logger, sseEvent.Id);
                    OnReload();
                }

                await SaveToCacheAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            firstConfigReceived.TrySetResult(false);
            return;
        }
        catch (Exception ex)
        {
            LogSseStreamError(_logger, ex);
        }

        firstConfigReceived.TrySetResult(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        switch (_options.ConnectionMode)
        {
            case ConnectionMode.Sse:
                await RunSseReconnectLoopAsync(cancellationToken).ConfigureAwait(false);
                break;

            case ConnectionMode.SseWithPollingFallback:
                LogSseDisconnectedSwitchingToPolling(_logger);
                await RunFallbackWithSseRetryAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: REST errors are logged and trigger cache fallback")]
    private async Task<bool> TryLoadFromFetcherAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _configFetcher.FetchAsync(null, cancellationToken).ConfigureAwait(false);
            if (result.Status == FetchStatus.Success && result.Config is not null)
            {
                ApplyConfig(result.Config);
                _currentRestETag = result.ETag;
                LogRestConfigLoaded(_logger);
                await SaveToCacheAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogRestFetchFailed(_logger, ex);
        }

        return false;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: cache errors are logged; provider starts with empty data")]
    private async Task TryLoadFromCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = await _configCache.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                ApplyConfig(cached);
                LogCacheConfigLoaded(_logger);
            }
        }
        catch (Exception ex)
        {
            LogCacheLoadFailed(_logger, ex);
        }
    }

    private void StartPolling()
    {
        if (_options.ConnectionMode == ConnectionMode.Sse)
        {
            return;
        }

        _backgroundTask = Task.Run(() => RunPollingLoopAsync(_cts.Token));
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: SSE reconnect errors are logged; backoff continues")]
    private async Task RunSseReconnectLoopAsync(CancellationToken cancellationToken)
    {
        var delay = _options.SseReconnectDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            var jitteredDelay = AddJitter(delay);
            LogSseReconnecting(_logger, jitteredDelay);

            await Task.Delay(jitteredDelay, cancellationToken).ConfigureAwait(false);

            try
            {
                _sseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var reconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var streamTask = StreamSseEventsAsync(reconnected, _sseCts.Token);

                if (await reconnected.Task.ConfigureAwait(false))
                {
                    LogSseReconnected(_logger);
                    _backgroundTask = streamTask;
                    return;
                }
            }
            catch (Exception ex)
            {
                LogSseStreamError(_logger, ex);
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _options.SseMaxReconnectDelay.Ticks));
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: SSE retry errors are logged; polling continues as fallback")]
    private async Task RunFallbackWithSseRetryAsync(CancellationToken cancellationToken)
    {
        using var pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pollingTask = Task.Run(() => RunPollingLoopAsync(pollingCts.Token), pollingCts.Token);

        var delay = _options.SseReconnectDelay;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(AddJitter(delay), cancellationToken).ConfigureAwait(false);

                try
                {
                    _sseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var reconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var sseTask = StreamSseEventsAsync(reconnected, _sseCts.Token);

                    if (await reconnected.Task.ConfigureAwait(false))
                    {
                        LogSseReconnected(_logger);
                        await pollingCts.CancelAsync().ConfigureAwait(false);
                        _backgroundTask = sseTask;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogSseStreamError(_logger, ex);
                }

                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _options.SseMaxReconnectDelay.Ticks));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down
        }
        finally
        {
            await pollingCts.CancelAsync().ConfigureAwait(false);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: polling errors are logged; next interval retries")]
    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(AddJitter(_options.PollingInterval), cancellationToken).ConfigureAwait(false);

                var result = await _configFetcher.FetchAsync(_currentRestETag, cancellationToken).ConfigureAwait(false);

                switch (result.Status)
                {
                    case FetchStatus.Success when result.Config is not null:
                        ApplyConfig(result.Config);
                        _currentRestETag = result.ETag;
                        LogPollingConfigUpdated(_logger);
                        OnReload();
                        await SaveToCacheAsync(cancellationToken).ConfigureAwait(false);
                        break;

                    case FetchStatus.NotModified:
                        LogPollingNotModified(_logger);
                        break;

                    case FetchStatus.AuthenticationError:
                        LogAuthFailureStoppingPolling(_logger);
                        return;

                    case FetchStatus.NotFound:
                        LogPollingNotFound(_logger);
                        break;

                    default:
                        LogPollingFetchFailed(_logger, null);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPollingFetchFailed(_logger, ex);
            }
        }
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Jitter for polling/reconnect intervals does not require cryptographic randomness")]
    private static TimeSpan AddJitter(TimeSpan baseDelay)
    {
        var jitterFactor = 0.75 + (Random.Shared.NextDouble() * 0.5);
        return TimeSpan.FromMilliseconds(Math.Max(baseDelay.TotalMilliseconds * jitterFactor, 100));
    }

    private void ApplyConfig(IReadOnlyDictionary<string, string> config)
    {
        var data = new Dictionary<string, string?>(config.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in config)
        {
            data[key] = value;
        }

        Data = data;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort; failures are logged at debug level")]
    private async Task SaveToCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in Data)
            {
                if (value is not null)
                {
                    snapshot[key] = value;
                }
            }

            await _configCache.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            LogCacheUpdated(_logger);
        }
        catch (Exception ex)
        {
            LogCacheUpdateFailed(_logger, ex);
        }
    }

    internal static IReadOnlyDictionary<string, string> ParseConfigData(string json) =>
        DefaultConfigFetcher.FlattenJson(json);

    internal static string? ParseSnapshotVersion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("snapshotVersion", out var version))
        {
            return version.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    // --- LoggerMessage definitions ---

    [LoggerMessage(1, LogLevel.Information, "Connected to GroundControl server via SSE.")]
    private static partial void LogSseConnected(ILogger logger);

    [LoggerMessage(2, LogLevel.Warning, "Startup timed out after {Timeout}. Using cached configuration if available.")]
    private static partial void LogStartupTimeout(ILogger logger, TimeSpan timeout);

    [LoggerMessage(15, LogLevel.Information, "SSE startup failed, falling back to REST.")]
    private static partial void LogSseStartupFailed(ILogger logger);

    [LoggerMessage(3, LogLevel.Information, "Configuration updated via SSE (version {Version}).")]
    private static partial void LogSseConfigUpdated(ILogger logger, string? version);

    [LoggerMessage(4, LogLevel.Warning, "SSE stream error.")]
    private static partial void LogSseStreamError(ILogger logger, Exception exception);

    [LoggerMessage(5, LogLevel.Information, "SSE disconnected, switching to polling mode.")]
    private static partial void LogSseDisconnectedSwitchingToPolling(ILogger logger);

    [LoggerMessage(6, LogLevel.Information, "Loaded configuration from REST endpoint.")]
    private static partial void LogRestConfigLoaded(ILogger logger);

    [LoggerMessage(7, LogLevel.Warning, "REST fetch failed during startup.")]
    private static partial void LogRestFetchFailed(ILogger logger, Exception exception);

    [LoggerMessage(8, LogLevel.Information, "Loaded configuration from local cache.")]
    private static partial void LogCacheConfigLoaded(ILogger logger);

    [LoggerMessage(9, LogLevel.Warning, "Failed to load configuration from cache.")]
    private static partial void LogCacheLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(10, LogLevel.Information, "Configuration updated via polling.")]
    private static partial void LogPollingConfigUpdated(ILogger logger);

    [LoggerMessage(11, LogLevel.Debug, "Polling: 304 Not Modified.")]
    private static partial void LogPollingNotModified(ILogger logger);

    [LoggerMessage(12, LogLevel.Warning, "Polling fetch failed, will retry at next interval.")]
    private static partial void LogPollingFetchFailed(ILogger logger, Exception? exception);

    [LoggerMessage(18, LogLevel.Error, "Authentication failed. Polling stopped permanently.")]
    private static partial void LogAuthFailureStoppingPolling(ILogger logger);

    [LoggerMessage(19, LogLevel.Debug, "Polling: no active snapshot (404).")]
    private static partial void LogPollingNotFound(ILogger logger);

    [LoggerMessage(13, LogLevel.Debug, "Local cache updated.")]
    private static partial void LogCacheUpdated(ILogger logger);

    [LoggerMessage(14, LogLevel.Debug, "Failed to update local cache.")]
    private static partial void LogCacheUpdateFailed(ILogger logger, Exception exception);

    [LoggerMessage(16, LogLevel.Information, "SSE reconnecting in {Delay}.")]
    private static partial void LogSseReconnecting(ILogger logger, TimeSpan delay);

    [LoggerMessage(17, LogLevel.Information, "SSE reconnected successfully.")]
    private static partial void LogSseReconnected(ILogger logger);
}