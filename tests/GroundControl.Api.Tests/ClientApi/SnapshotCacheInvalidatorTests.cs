using GroundControl.Api.Features.ClientApi;
using GroundControl.Api.Shared.Notification;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GroundControl.Api.Tests.ClientApi;

public sealed class SnapshotCacheInvalidatorTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly ISnapshotStore _snapshotStore = Substitute.For<ISnapshotStore>();

    [Fact]
    public async Task ExecuteAsync_ChangeNotification_InvalidatesCache()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();
        var snapshot = new Snapshot
        {
            Id = snapshotId,
            ProjectId = projectId,
            SnapshotVersion = 1,
            Entries = [],
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = Guid.CreateVersion7(),
        };

        _snapshotStore.GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var cache = new SnapshotCache(_snapshotStore);
        await using var notifier = new InProcessChangeNotifier();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);

        using var invalidator = new SnapshotCacheInvalidator(
            cache,
            notifier,
            NullLogger<SnapshotCacheInvalidator>.Instance);

        // Pre-populate cache
        await cache.GetOrLoadAsync(projectId, TestCancellationToken);
        _snapshotStore.ClearReceivedCalls();

        // Start the invalidator
        await invalidator.StartAsync(cts.Token);

        // Give the background service time to subscribe
        await Task.Delay(50, TestCancellationToken);

        // Act — send a change notification
        await notifier.NotifyAsync(projectId, snapshotId, TestCancellationToken);

        // Give time for the invalidation to process
        await Task.Delay(100, TestCancellationToken);

        // Assert — store was called again due to invalidation
        await _snapshotStore.Received(1).GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>());

        // Cleanup
        await cts.CancelAsync();
        await IgnoreOperationCanceledException(invalidator.StopAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_StoppingToken_StopsGracefully()
    {
        // Arrange
        var cache = new SnapshotCache(_snapshotStore);
        await using var notifier = new InProcessChangeNotifier();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);

        using var invalidator = new SnapshotCacheInvalidator(
            cache,
            notifier,
            NullLogger<SnapshotCacheInvalidator>.Instance);

        await invalidator.StartAsync(cts.Token);
        await Task.Delay(50, TestCancellationToken);

        // Act
        await cts.CancelAsync();

        // Assert — should not throw
        await IgnoreOperationCanceledException(invalidator.StopAsync(CancellationToken.None));
    }

    private static async Task IgnoreOperationCanceledException(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}