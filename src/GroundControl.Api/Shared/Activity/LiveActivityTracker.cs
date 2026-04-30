using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Activity;

internal sealed class LiveActivityTracker : ILiveActivityTracker
{
    private const int RateWindowSize = 10;
    private const int SubscriberBufferSize = 20;

    private readonly ConcurrentDictionary<Guid, ChannelWriter<LiveActivityEvent>> _subscribers = new();
    private readonly ConcurrentQueue<long> _eventTimestamps = new();
    private int _clientCount;
    private volatile bool _disposed;

    public LiveActivitySnapshot Current => GetSnapshot();

    public void ClientConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Interlocked.Increment(ref _clientCount);
        RecordEvent();
    }

    public void ClientDisconnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Interlocked.Decrement(ref _clientCount) < 0)
        {
            Interlocked.Exchange(ref _clientCount, 0);
        }

        RecordEvent();
    }

    public void RecordEvent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _eventTimestamps.Enqueue(Stopwatch.GetTimestamp());
        Publish(LiveActivityEvent.FromActivity(GetSnapshot()));
    }

    public void RecordAuditRecord(AuditRecord record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(record);

        _eventTimestamps.Enqueue(Stopwatch.GetTimestamp());
        Publish(LiveActivityEvent.FromActivity(GetSnapshot()));
        Publish(LiveActivityEvent.FromAuditRecord(record));
    }

    public IAsyncEnumerable<LiveActivityEvent> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<LiveActivityEvent>(new BoundedChannelOptions(SubscriberBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        var id = Guid.CreateVersion7();
        _subscribers.TryAdd(id, channel.Writer);
        channel.Writer.TryWrite(LiveActivityEvent.FromActivity(GetSnapshot()));

        return ReadSubscriptionAsync(channel, id, cancellationToken);
    }

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

    private async IAsyncEnumerable<LiveActivityEvent> ReadSubscriptionAsync(
        Channel<LiveActivityEvent> channel,
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

    private void Publish(LiveActivityEvent item)
    {
        foreach (var writer in _subscribers.Values)
        {
            writer.TryWrite(item);
        }
    }

    private LiveActivitySnapshot GetSnapshot()
    {
        TrimRateWindow();
        var timestamps = _eventTimestamps.ToArray();

        return new LiveActivitySnapshot
        {
            Clients = Math.Max(0, Volatile.Read(ref _clientCount)),
            Rate = ComputeRate(timestamps),
        };
    }

    private void TrimRateWindow()
    {
        while (_eventTimestamps.Count > RateWindowSize)
        {
            _eventTimestamps.TryDequeue(out _);
        }
    }

    private static double ComputeRate(long[] timestamps)
    {
        if (timestamps.Length < 2)
        {
            return 0;
        }

        var elapsedTicks = timestamps[^1] - timestamps[0];
        if (elapsedTicks <= 0)
        {
            return 0;
        }

        return timestamps.Length / (elapsedTicks / (double)Stopwatch.Frequency);
    }
}