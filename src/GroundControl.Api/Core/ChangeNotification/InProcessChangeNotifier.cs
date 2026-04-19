using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace GroundControl.Api.Core.ChangeNotification;

/// <summary>
/// In-memory change notifier using per-subscriber fan-out via <see cref="Channel{T}"/>.
/// Each subscriber gets its own channel so all active subscribers independently receive every notification.
/// </summary>
internal sealed class InProcessChangeNotifier : IChangeNotifier
{
    private readonly ConcurrentDictionary<Guid, ChannelWriter<(Guid ProjectId, Guid SnapshotId)>> _subscribers = new();
    private volatile bool _disposed;

    /// <inheritdoc />
    public async Task NotifyAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var writer in _subscribers.Values)
        {
            await writer.WriteAsync((projectId, snapshotId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<(Guid ProjectId, Guid SnapshotId)> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        // Register the subscriber synchronously so a notification published between
        // this call and the caller's first MoveNextAsync is not lost.
        var channel = Channel.CreateUnbounded<(Guid, Guid)>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        var id = Guid.CreateVersion7();
        _subscribers.TryAdd(id, channel.Writer);

        return ReadSubscriptionAsync(channel, id, cancellationToken);
    }

    private async IAsyncEnumerable<(Guid ProjectId, Guid SnapshotId)> ReadSubscriptionAsync(
        Channel<(Guid, Guid)> channel,
        Guid id,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Gets whether the notifier has been disposed.
    /// </summary>
    internal bool IsDisposed => _disposed;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        foreach (var writer in _subscribers.Values)
        {
            writer.TryComplete();
        }

        _subscribers.Clear();
        return ValueTask.CompletedTask;
    }
}