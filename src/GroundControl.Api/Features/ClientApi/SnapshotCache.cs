using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ClientApi;

/// <summary>
/// In-memory cache for active project snapshots, keyed by project ID.
/// Loads lazily from the snapshot store on first access and supports invalidation via change notifications.
/// </summary>
internal sealed class SnapshotCache : IDisposable
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly ConcurrentDictionary<Guid, Snapshot?> _cache = new();
    private readonly Meter _meter;
    private readonly Counter<long> _hitCounter;
    private readonly Counter<long> _missCounter;

    public SnapshotCache(ISnapshotStore snapshotStore, IMeterFactory meterFactory)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));

        _meter = meterFactory.Create("GroundControl");
        _hitCounter = _meter.CreateCounter<long>("groundcontrol.cache.hits", description: "Number of snapshot cache hits");
        _missCounter = _meter.CreateCounter<long>("groundcontrol.cache.misses", description: "Number of snapshot cache misses");
    }

    /// <summary>
    /// Gets the active snapshot for a project, loading from the store on first access.
    /// </summary>
    public async Task<Snapshot?> GetOrLoadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(projectId, out var cached))
        {
            _hitCounter.Add(1);
            return cached;
        }

        _missCounter.Add(1);
        var snapshot = await _snapshotStore.GetActiveForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        _cache[projectId] = snapshot;
        return snapshot;
    }

    /// <summary>
    /// Invalidates the cached snapshot for a project and reloads from the store.
    /// </summary>
    public async Task InvalidateAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(projectId, out _);
        var snapshot = await _snapshotStore.GetActiveForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        _cache[projectId] = snapshot;
    }

    public void Dispose() => _meter.Dispose();
}