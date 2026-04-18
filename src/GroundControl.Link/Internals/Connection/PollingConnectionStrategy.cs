using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Internals.Connection;

/// <summary>
/// Periodic REST polling strategy with jitter.
/// </summary>
internal sealed partial class PollingConnectionStrategy : IConnectionStrategy
{
    private readonly IGroundControlApiClient _client;
    private readonly IConfigurationCache _cache;
    private readonly ILogger<PollingConnectionStrategy> _logger;
    private readonly GroundControlMetrics _metrics;

    public PollingConnectionStrategy(
        IGroundControlApiClient client,
        IConfigurationCache cache,
        ILogger<PollingConnectionStrategy> logger,
        GroundControlMetrics metrics)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Polling loop must survive transient errors")]
    public async Task ExecuteAsync(GroundControlStore store, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sw = Stopwatch.GetTimestamp();
                var result = await _client.FetchConfigAsync(store.GetSnapshot().ETag, cancellationToken).ConfigureAwait(false);
                _metrics.RecordFetchDuration(Stopwatch.GetElapsedTime(sw));

                switch (result.Status)
                {
                    case FetchStatus.Success when result.Config is not null:
                        store.Update(new Dictionary<string, ConfigValue>(result.Config, StringComparer.OrdinalIgnoreCase), result.ETag, null);
                        _metrics.RecordFetch("success");
                        _metrics.RecordReload("polling");
                        await TrySaveToCacheAsync(result.Config, result.ETag, cancellationToken).ConfigureAwait(false);
                        break;

                    case FetchStatus.NotModified:
                        _metrics.RecordFetch("not_modified");
                        break;

                    case FetchStatus.AuthenticationError:
                        LogAuthFailure(_logger);
                        _metrics.RecordFetch("auth_error");
                        return;

                    case FetchStatus.TransientError:
                    case FetchStatus.NotFound:
                    default:
                        _metrics.RecordFetch("error");
                        store.SetHealth(HealthStatus.Degraded);
                        break;
                }

                await Task.Delay(ConnectionHelpers.AddJitter(store.Options.PollingInterval), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPollFailed(_logger, ex);
                _metrics.RecordFetch("error");

                store.SetHealth(HealthStatus.Degraded);
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cache save is best-effort")]
    private async Task TrySaveToCacheAsync(IReadOnlyDictionary<string, ConfigValue> data, string? etag, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SaveAsync(
                    new CachedConfiguration
                    {
                        Entries = new Dictionary<string, ConfigValue>(data, StringComparer.OrdinalIgnoreCase),
                        ETag = etag
                    },
                    cancellationToken)
                .ConfigureAwait(false);
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