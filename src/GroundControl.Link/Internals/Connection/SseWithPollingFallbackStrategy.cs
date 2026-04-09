using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Link.Internals.Connection;

/// <summary>
/// Hybrid strategy: attempts SSE first, falls back to polling on failure,
/// and periodically retries SSE reconnection while polling continues.
/// </summary>
internal sealed partial class SseWithPollingFallbackStrategy : IConnectionStrategy
{
    private readonly IGroundControlSseClient _sseClient;
    private readonly IGroundControlApiClient _client;
    private readonly IConfigurationCache _cache;
    private readonly ILogger<SseWithPollingFallbackStrategy> _logger;
    private readonly GroundControlMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;

    public SseWithPollingFallbackStrategy(
        IGroundControlSseClient sseClient,
        IGroundControlApiClient client,
        IConfigurationCache cache,
        ILogger<SseWithPollingFallbackStrategy> logger,
        GroundControlMetrics metrics,
        IServiceProvider serviceProvider)
    {
        _sseClient = sseClient;
        _client = client;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
        _serviceProvider = serviceProvider;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: errors trigger fallback, not crash")]
    public async Task ExecuteAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        try
        {
            _metrics.SetSseConnected(true);
            await StreamSseEventsAsync(store, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            LogSseStreamError(_logger, ex);
        }
        finally
        {
            _metrics.SetSseConnected(false);
        }

        LogSwitchingToPolling(_logger);
        await RunPollingWithSseRetryAsync(store, cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort")]
    private async Task StreamSseEventsAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        await foreach (var sseEvent in _sseClient.StreamAsync(cancellationToken).ConfigureAwait(false))
        {
            if (sseEvent.EventType != SseEventType.Config)
            {
                continue;
            }

            var (config, snapshotVersion) = ConnectionHelpers.ParseConfigDataWithVersion(sseEvent.Data);
            store.Update(config, snapshotVersion, sseEvent.Id);
            _sseClient.LastEventId = sseEvent.Id;
            _metrics.RecordReload("sse");

            try
            {
                await _cache.SaveAsync(
                        new CachedConfiguration
                        {
                            Entries = config,
                            ETag = snapshotVersion,
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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Resilience: SSE retry errors are logged; polling continues")]
    private async Task RunPollingWithSseRetryAsync(GroundControlStore store, CancellationToken stoppingToken)
    {
        using var pollingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var pollingStrategy = _serviceProvider.GetRequiredService<PollingConnectionStrategy>();
        var pollingTask = Task.Run(() => pollingStrategy.ExecuteAsync(store, pollingCts.Token), pollingCts.Token);

        var delay = store.Options.SseReconnectDelay;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ConnectionHelpers.AddJitter(delay), stoppingToken).ConfigureAwait(false);

                    _metrics.RecordSseReconnect();
                    _metrics.SetSseConnected(true);

                    await StreamSseEventsAsync(store, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogSseRetryFailed(_logger, ex);
                }
                finally
                {
                    _metrics.SetSseConnected(false);
                }

                delay = TimeSpan.FromTicks(
                    Math.Min(delay.Ticks * 2, store.Options.SseMaxReconnectDelay.Ticks));
            }
        }
        finally
        {
            await pollingCts.CancelAsync().ConfigureAwait(false);
        }
    }

    [LoggerMessage(1, LogLevel.Warning, "SSE stream error.")]
    private static partial void LogSseStreamError(ILogger logger, Exception exception);

    [LoggerMessage(2, LogLevel.Information, "SSE disconnected, switching to polling with periodic SSE retry.")]
    private static partial void LogSwitchingToPolling(ILogger logger);

    [LoggerMessage(3, LogLevel.Debug, "SSE retry failed, continuing polling.")]
    private static partial void LogSseRetryFailed(ILogger logger, Exception exception);
}