namespace GroundControl.Api.Shared.Notification;

/// <summary>
/// Notifies subscribers when a project's active snapshot changes.
/// </summary>
public interface IChangeNotifier : IAsyncDisposable
{
    /// <summary>
    /// Notifies subscribers that a new snapshot has been activated for a project.
    /// </summary>
    Task NotifyAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to snapshot change notifications.
    /// </summary>
    IAsyncEnumerable<(Guid ProjectId, Guid SnapshotId)> SubscribeAsync(CancellationToken cancellationToken = default);
}