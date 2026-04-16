using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Internals.Connection;

/// <summary>
/// Hybrid strategy: attempts SSE first, falls back to polling on failure,
/// and periodically retries SSE reconnection while polling continues.
/// </summary>
internal sealed partial class SseWithPollingFallbackStrategy : IConnectionStrategy
{
    private readonly IGroundControlSseClient _sseClient;
    private readonly IConfigurationCache _cache;
    private readonly ILogger<SseWithPollingFallbackStrategy> _logger;
    private readonly GroundControlMetrics _metrics;
    private readonly PollingConnectionStrategy _pollingStrategy;

    public SseWithPollingFallbackStrategy(
        IGroundControlSseClient sseClient,
        IConfigurationCache cache,
        ILogger<SseWithPollingFallbackStrategy> logger,
        GroundControlMetrics metrics,
        PollingConnectionStrategy pollingStrategy)
    {
        _sseClient = sseClient;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
        _pollingStrategy = pollingStrategy;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: errors trigger fallback, not crash")]
    public async Task ExecuteAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        try
        {
            await StreamSseEventsAsync(store, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            LogSseStreamError(_logger, ex);
            store.SetHealth(HealthStatus.Degraded);
        }

        LogSwitchingToPolling(_logger);
        await RunPollingWithSseRetryAsync(store, cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort")]
    private async Task<bool> StreamSseEventsAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        var receivedEvents = false;
        _metrics.SetSseConnected(true);

        try
        {
            await foreach (var sseEvent in _sseClient.StreamAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (sseEvent.EventType != SseEventType.Config)
                {
                    continue;
                }

                var parsed = ConfigurationParser.Parse(sseEvent.Data);
                store.Update(parsed.Config, parsed.SnapshotVersion, sseEvent.Id);

                _sseClient.LastEventId = sseEvent.Id;
                _metrics.RecordReload("sse");
                receivedEvents = true;

                try
                {
                    await _cache.SaveAsync(
                            new CachedConfiguration
                            {
                                Entries = parsed.Config,
                                ETag = parsed.SnapshotVersion,
                                LastEventId = sseEvent.Id
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort
                }
            }
        }
        finally
        {
            _metrics.SetSseConnected(false);
        }

        return receivedEvents;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: SSE retry errors are logged; polling continues")]
    private async Task RunPollingWithSseRetryAsync(GroundControlStore store, CancellationToken stoppingToken)
    {
        using var pollingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        // Justification: pollingTask is awaited in the finally block before pollingCts is disposed
        // ReSharper disable once AccessToDisposedClosure
        var pollingTask = Task.Run(() => _pollingStrategy.ExecuteAsync(store, pollingCts.Token), pollingCts.Token);

        var delay = store.Options.SseReconnectDelay;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ConnectionHelpers.AddJitter(delay), stoppingToken).ConfigureAwait(false);

                    _metrics.RecordSseReconnect();
                    var receivedEvents = await StreamSseEventsAsync(store, stoppingToken).ConfigureAwait(false);

                    delay = receivedEvents
                        ? store.Options.SseReconnectDelay
                        : TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, store.Options.SseMaxReconnectDelay.Ticks));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogSseRetryFailed(_logger, ex);
                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, store.Options.SseMaxReconnectDelay.Ticks));
                }
            }
        }
        finally
        {
            await pollingCts.CancelAsync().ConfigureAwait(false);

            try
            {
                await pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    [LoggerMessage(1, LogLevel.Warning, "SSE stream error.")]
    private static partial void LogSseStreamError(ILogger logger, Exception exception);

    [LoggerMessage(2, LogLevel.Information, "SSE disconnected, switching to polling with periodic SSE retry.")]
    private static partial void LogSwitchingToPolling(ILogger logger);

    [LoggerMessage(3, LogLevel.Debug, "SSE retry failed, continuing polling.")]
    private static partial void LogSseRetryFailed(ILogger logger, Exception exception);
}