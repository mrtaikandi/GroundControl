using System.Runtime.CompilerServices;

namespace GroundControl.Api.Core.ChangeNotification;

internal sealed class NullChangeNotifier : IChangeNotifier
{
    public Task NotifyAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<(Guid ProjectId, Guid SnapshotId)> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        yield break;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}