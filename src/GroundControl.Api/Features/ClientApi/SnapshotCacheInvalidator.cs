using GroundControl.Api.Core.ChangeNotification;

namespace GroundControl.Api.Features.ClientApi;

/// <summary>
/// Background service that subscribes to snapshot change notifications
/// and invalidates the corresponding cache entries.
/// </summary>
internal sealed partial class SnapshotCacheInvalidator : BackgroundService
{
    private readonly SnapshotCache _cache;
    private readonly IChangeNotifier _changeNotifier;
    private readonly ILogger<SnapshotCacheInvalidator> _logger;

    public SnapshotCacheInvalidator(SnapshotCache cache, IChangeNotifier changeNotifier, ILogger<SnapshotCacheInvalidator> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _changeNotifier = changeNotifier ?? throw new ArgumentNullException(nameof(changeNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        try
        {
            await foreach (var (projectId, snapshotId) in _changeNotifier.SubscribeAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    LogInvalidating(_logger, projectId, snapshotId);
                    await _cache.InvalidateAsync(projectId, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogInvalidationFailed(_logger, ex, projectId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        LogStopped(_logger);
    }

    [LoggerMessage(1, LogLevel.Information, "Snapshot cache invalidator started.")]
    private static partial void LogStarted(ILogger<SnapshotCacheInvalidator> logger);

    [LoggerMessage(2, LogLevel.Debug, "Invalidating cache for project {ProjectId} due to snapshot {SnapshotId}.")]
    private static partial void LogInvalidating(ILogger<SnapshotCacheInvalidator> logger, Guid projectId, Guid snapshotId);

    [LoggerMessage(3, LogLevel.Error, "Failed to invalidate cache for project {ProjectId}.")]
    private static partial void LogInvalidationFailed(ILogger<SnapshotCacheInvalidator> logger, Exception exception, Guid projectId);

    [LoggerMessage(4, LogLevel.Information, "Snapshot cache invalidator stopped.")]
    private static partial void LogStopped(ILogger<SnapshotCacheInvalidator> logger);
}