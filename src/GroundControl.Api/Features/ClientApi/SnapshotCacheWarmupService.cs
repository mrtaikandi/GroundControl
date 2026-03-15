using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ClientApi;

/// <summary>
/// Hosted service that pre-warms the snapshot cache on startup by loading
/// active snapshots for all projects. Enabled when <c>Cache:PrewarmOnStartup</c> is <c>true</c>.
/// </summary>
internal sealed partial class SnapshotCacheWarmupService : IHostedService
{
    private readonly SnapshotCache _cache;
    private readonly IProjectStore _projectStore;
    private readonly ILogger<SnapshotCacheWarmupService> _logger;

    public SnapshotCacheWarmupService(SnapshotCache cache, IProjectStore projectStore, ILogger<SnapshotCacheWarmupService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogPrewarmStarted(_logger);

        var warmed = 0;
        string? cursor = null;

        do
        {
            var query = new ProjectListQuery { Limit = 100, After = cursor };
            var page = await _projectStore.ListAsync(query, cancellationToken).ConfigureAwait(false);

            foreach (var project in page.Items)
            {
                if (project.ActiveSnapshotId is null)
                {
                    continue;
                }

                await _cache.GetOrLoadAsync(project.Id, cancellationToken).ConfigureAwait(false);
                warmed++;
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);

        LogPrewarmCompleted(_logger, warmed);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(1, LogLevel.Information, "Pre-warming snapshot cache.")]
    private static partial void LogPrewarmStarted(ILogger<SnapshotCacheWarmupService> logger);

    [LoggerMessage(2, LogLevel.Information, "Snapshot cache pre-warmed with {Count} project(s).")]
    private static partial void LogPrewarmCompleted(ILogger<SnapshotCacheWarmupService> logger, int count);
}