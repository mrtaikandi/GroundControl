using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for snapshot entities. Snapshots are immutable once created.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Gets a snapshot by its unique identifier.
    /// </summary>
    Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists snapshots for a project with paging and sorting.
    /// </summary>
    Task<PagedResult<Snapshot>> ListAsync(SnapshotListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new snapshot.
    /// </summary>
    Task CreateAsync(Snapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active snapshot for a project.
    /// </summary>
    Task<Snapshot?> GetActiveForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next version number for a snapshot in a project.
    /// </summary>
    Task<long> GetNextVersionAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old snapshots for a project, retaining the specified count and the active snapshot.
    /// </summary>
    Task DeleteOldSnapshotsAsync(Guid projectId, int retentionCount, Guid? activeSnapshotId, CancellationToken cancellationToken = default);
}