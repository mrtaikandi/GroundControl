using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Link.Internals;

/// <summary>
/// Periodic REST polling strategy with jitter.
/// </summary>
internal sealed partial class PollingConnectionStrategy : IConnectionStrategy
{
    private readonly IConfigFetcher _fetcher;
    private readonly IConfigCache _cache;
    private readonly ILogger<PollingConnectionStrategy> _logger;
    private readonly GroundControlMetrics _metrics;

    public PollingConnectionStrategy(
        IConfigFetcher fetcher,
        IConfigCache cache,
        ILogger<PollingConnectionStrategy> logger,
        GroundControlMetrics metrics)
    {
        _fetcher = fetcher;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Polling loop must survive transient errors")]
    public async Task ExecuteAsync(GroundControlStore store, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    ConnectionHelpers.AddJitter(store.Options.PollingInterval),
                    stoppingToken).ConfigureAwait(false);

                var sw = Stopwatch.StartNew();
                var result = await _fetcher.FetchAsync(
                    store.GetSnapshot().ETag, stoppingToken).ConfigureAwait(false);
                sw.Stop();
                _metrics.RecordFetchDuration(sw.Elapsed.TotalSeconds);

                switch (result.Status)
                {
                    case FetchStatus.Success when result.Config is not null:
                        store.Update(
                            new Dictionary<string, string>(result.Config, StringComparer.OrdinalIgnoreCase),
                            result.ETag, null);
                        _metrics.RecordFetch("success");
                        _metrics.RecordReload("polling");
                        await TrySaveToCacheAsync(result.Config, result.ETag, stoppingToken)
                            .ConfigureAwait(false);
                        break;

                    case FetchStatus.NotModified:
                        _metrics.RecordFetch("not_modified");
                        break;

                    case FetchStatus.AuthenticationError:
                        LogAuthFailure(_logger);
                        _metrics.RecordFetch("auth_error");
                        return;

                    default:
                        _metrics.RecordFetch("error");
                        store.SetHealth(StoreHealthStatus.Degraded);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPollFailed(_logger, ex);
                _metrics.RecordFetch("error");
                store.SetHealth(StoreHealthStatus.Degraded);
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort")]
    private async Task TrySaveToCacheAsync(
        IReadOnlyDictionary<string, string> data, string? etag, CancellationToken ct)
    {
        try
        {
            await _cache.SaveAsync(
                new CachedConfiguration
                {
                    Entries = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase),
                    ETag = etag
                }, ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort
        }
    }

    [LoggerMessage(1, LogLevel.Error, "Authentication failed. Polling stopped permanently.")]
    private static partial void LogAuthFailure(ILogger logger);

    [LoggerMessage(2, LogLevel.Warning, "Polling fetch failed, will retry at next interval.")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}