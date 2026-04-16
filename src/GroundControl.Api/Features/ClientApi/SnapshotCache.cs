using System.Collections.Concurrent;
using GroundControl.Api.Shared.Observability;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ClientApi;

/// <summary>
/// In-memory cache for active project snapshots, keyed by project ID.
/// Loads lazily from the snapshot store on first access and supports invalidation via change notifications.
/// </summary>
internal sealed class SnapshotCache
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly IProjectStore _projectStore;
    private readonly ConcurrentDictionary<Guid, Snapshot?> _cache = new();

    public SnapshotCache(ISnapshotStore snapshotStore, IProjectStore projectStore)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
    }

    /// <summary>
    /// Gets the active snapshot for a project, loading from the store on first access.
    /// </summary>
    public async Task<Snapshot?> GetOrLoadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(projectId, out var cached))
        {
            GroundControlMetrics.CacheHits.Add(1);
            return cached;
        }

        GroundControlMetrics.CacheMisses.Add(1);
        var snapshot = await LoadActiveAsync(projectId, cancellationToken).ConfigureAwait(false);
        _cache[projectId] = snapshot;
        return snapshot;
    }

    /// <summary>
    /// Invalidates the cached snapshot for a project, reloads from the store, and returns the fresh snapshot.
    /// </summary>
    public async Task<Snapshot?> InvalidateAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(projectId, out _);
        var snapshot = await LoadActiveAsync(projectId, cancellationToken).ConfigureAwait(false);
        _cache[projectId] = snapshot;

        return snapshot;
    }

    private async Task<Snapshot?> LoadActiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _projectStore.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project?.ActiveSnapshotId is { } activeId)
        {
            return await _snapshotStore.GetByIdAsync(activeId, cancellationToken).ConfigureAwait(false);
        }

        return await _snapshotStore.GetActiveForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
    }
}