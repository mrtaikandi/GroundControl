using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Internals;

/// <summary>
/// SSE-only connection strategy with exponential backoff reconnection.
/// </summary>
internal sealed partial class SseConnectionStrategy : IConnectionStrategy
{
    private readonly ISseClient _sseClient;
    private readonly IConfigCache _cache;
    private readonly ILogger<SseConnectionStrategy> _logger;
    private readonly GroundControlMetrics _metrics;

    public SseConnectionStrategy(
        ISseClient sseClient,
        IConfigCache cache,
        ILogger<SseConnectionStrategy> logger,
        GroundControlMetrics metrics)
    {
        _sseClient = sseClient;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Reconnect loop must survive transient errors")]
    public async Task ExecuteAsync(GroundControlStore store, CancellationToken stoppingToken)
    {
        var delay = store.Options.SseReconnectDelay;
        var firstAttempt = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!firstAttempt)
                {
                    var jitteredDelay = ConnectionHelpers.AddJitter(delay);
                    LogReconnecting(_logger, jitteredDelay);
                    _metrics.RecordSseReconnect();
                    await Task.Delay(jitteredDelay, stoppingToken).ConfigureAwait(false);
                }

                firstAttempt = false;
                await StreamEventsAsync(store, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                firstAttempt = false;
                LogStreamError(_logger, ex);
                store.SetHealth(HealthStatus.Degraded);
            }

            delay = TimeSpan.FromTicks(
                Math.Min(delay.Ticks * 2, store.Options.SseMaxReconnectDelay.Ticks));
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort")]
    internal async Task StreamEventsAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        _metrics.SetSseConnected(true);

        try
        {
            await foreach (var sseEvent in _sseClient.StreamAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (sseEvent.EventType != "config")
                {
                    continue;
                }

                var (config, snapshotVersion) = ConnectionHelpers.ParseConfigDataWithVersion(sseEvent.Data);
                store.Update(config, snapshotVersion, sseEvent.Id);
                _sseClient.LastEventId = sseEvent.Id;
                _metrics.RecordReload("sse");

                try
                {
                    var cached = new CachedConfiguration
                    {
                        Entries = config,
                        ETag = snapshotVersion,
                        LastEventId = sseEvent.Id
                    };
                    await _cache.SaveAsync(cached, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort cache save
                }
            }
        }
        finally
        {
            _metrics.SetSseConnected(false);
        }
    }

    [LoggerMessage(1, LogLevel.Information, "SSE reconnecting in {Delay}.")]
    private static partial void LogReconnecting(ILogger logger, TimeSpan delay);

    [LoggerMessage(2, LogLevel.Warning, "SSE stream error.")]
    private static partial void LogStreamError(ILogger logger, Exception exception);
}