using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GroundControl.Api.Shared.Observability;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GroundControl.Api.Shared.Notification;

/// <summary>
/// Change notifier that watches the MongoDB <c>projects</c> collection change stream
/// for <c>activeSnapshotId</c> updates and delivers notifications to local subscribers.
/// Also supports direct local notification via <see cref="NotifyAsync"/>.
/// </summary>
internal sealed partial class MongoChangeStreamNotifier : IChangeNotifier, IHostedService
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly IMongoCollection<Project> _collection;
    private readonly ILogger<MongoChangeStreamNotifier> _logger;
    private readonly ConcurrentDictionary<Guid, ChannelWriter<(Guid ProjectId, Guid SnapshotId)>> _subscribers = new();

    private CancellationTokenSource? _cts;
    private Task? _watchTask;
    private BsonDocument? _resumeToken;
    private volatile bool _isConnected;
    private volatile bool _disposed;

    public MongoChangeStreamNotifier(IMongoDbContext context, ILogger<MongoChangeStreamNotifier> logger)
    {
        _collection = context.GetCollection<Project>("projects");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether the change stream cursor is actively connected.
    /// </summary>
    internal bool IsConnected => _isConnected;

    /// <summary>
    /// Gets whether the notifier has been disposed.
    /// </summary>
    internal bool IsDisposed => _disposed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _watchTask = WatchChangeStreamAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);

            if (_watchTask is not null)
            {
                try
                {
                    await _watchTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown timeout exceeded or task cancelled — expected
                }
            }

            _cts.Dispose();
            _cts = null;
        }
    }

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
    public async IAsyncEnumerable<(Guid ProjectId, Guid SnapshotId)> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<(Guid, Guid)>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        var id = Guid.CreateVersion7();
        _subscribers.TryAdd(id, channel.Writer);

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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        _cts?.Dispose();
        _cts = null;

        foreach (var writer in _subscribers.Values)
        {
            writer.TryComplete();
        }

        _subscribers.Clear();
        return ValueTask.CompletedTask;
    }

    private async Task WatchChangeStreamAsync(CancellationToken cancellationToken)
    {
        var backoff = InitialBackoff;

        LogStarted(_logger);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var filter = Builders<ChangeStreamDocument<Project>>.Filter.Eq("operationType", "update")
                    & Builders<ChangeStreamDocument<Project>>.Filter.Exists("updateDescription.updatedFields.activeSnapshotId");

                var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<Project>>()
                    .Match(filter);

                var options = new ChangeStreamOptions
                {
                    FullDocument = ChangeStreamFullDocumentOption.UpdateLookup,
                    ResumeAfter = _resumeToken
                };

                using var cursor = await _collection.WatchAsync(pipeline, options, cancellationToken).ConfigureAwait(false);

                _isConnected = true;
                backoff = InitialBackoff;
                LogConnected(_logger);

                while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                {
                    foreach (var change in cursor.Current)
                    {
                        _resumeToken = change.ResumeToken;

                        if (change.FullDocument?.ActiveSnapshotId is not { } snapshotId)
                        {
                            continue;
                        }

                        var projectId = change.FullDocument.Id;
                        LogChangeDetected(_logger, projectId, snapshotId);
                        GroundControlMetrics.ChangeNotifierEvents.Add(1);

                        foreach (var writer in _subscribers.Values)
                        {
                            await writer.WriteAsync((projectId, snapshotId), cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (MongoException ex)
            {
                _isConnected = false;
                LogDisconnected(_logger, ex, backoff);

                try
                {
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }

        _isConnected = false;
        LogStopped(_logger);
    }

    [LoggerMessage(1, LogLevel.Information, "MongoDB change stream notifier started.")]
    private static partial void LogStarted(ILogger<MongoChangeStreamNotifier> logger);

    [LoggerMessage(2, LogLevel.Information, "Connected to MongoDB change stream.")]
    private static partial void LogConnected(ILogger<MongoChangeStreamNotifier> logger);

    [LoggerMessage(3, LogLevel.Debug, "Change stream detected activeSnapshotId update for project {ProjectId} to snapshot {SnapshotId}.")]
    private static partial void LogChangeDetected(ILogger<MongoChangeStreamNotifier> logger, Guid projectId, Guid snapshotId);

    [LoggerMessage(4, LogLevel.Warning, "Change stream disconnected. Reconnecting in {Backoff}...")]
    private static partial void LogDisconnected(ILogger<MongoChangeStreamNotifier> logger, Exception exception, TimeSpan backoff);

    [LoggerMessage(5, LogLevel.Information, "MongoDB change stream notifier stopped.")]
    private static partial void LogStopped(ILogger<MongoChangeStreamNotifier> logger);
}