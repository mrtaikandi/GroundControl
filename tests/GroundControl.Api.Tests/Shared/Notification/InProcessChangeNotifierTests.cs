using GroundControl.Api.Shared.Notification;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Notification;

public sealed class InProcessChangeNotifierTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NotifyAsync_SingleSubscriber_ReceivesNotification()
    {
        // Arrange
        await using var notifier = new InProcessChangeNotifier();
        var projectId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var received = new List<(Guid ProjectId, Guid SnapshotId)>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts.Token))
            {
                received.Add(item);
                await cts.CancelAsync();
            }
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.NotifyAsync(projectId, snapshotId, TestCancellationToken);

        await IgnoreOperationCanceledException(subscriberTask);

        // Assert
        received.ShouldHaveSingleItem();
        received[0].ProjectId.ShouldBe(projectId);
        received[0].SnapshotId.ShouldBe(snapshotId);
    }

    [Fact]
    public async Task NotifyAsync_MultipleSubscribers_AllReceiveNotification()
    {
        // Arrange
        await using var notifier = new InProcessChangeNotifier();
        var projectId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();

        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cts3 = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);

        var received1 = new List<(Guid ProjectId, Guid SnapshotId)>();
        var received2 = new List<(Guid ProjectId, Guid SnapshotId)>();
        var received3 = new List<(Guid ProjectId, Guid SnapshotId)>();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts1.Token))
            {
                received1.Add(item);
                await cts1.CancelAsync();
            }
        }, TestCancellationToken);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts2.Token))
            {
                received2.Add(item);
                await cts2.CancelAsync();
            }
        }, TestCancellationToken);

        var sub3 = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts3.Token))
            {
                received3.Add(item);
                await cts3.CancelAsync();
            }
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.NotifyAsync(projectId, snapshotId, TestCancellationToken);

        await IgnoreOperationCanceledException(Task.WhenAll(sub1, sub2, sub3));

        // Assert
        received1.ShouldHaveSingleItem();
        received2.ShouldHaveSingleItem();
        received3.ShouldHaveSingleItem();
        received1[0].ShouldBe((projectId, snapshotId));
        received2[0].ShouldBe((projectId, snapshotId));
        received3[0].ShouldBe((projectId, snapshotId));
    }

    [Fact]
    public async Task SubscribeAsync_OnlyReceivesNotificationsPublishedAfterSubscribing()
    {
        // Arrange
        await using var notifier = new InProcessChangeNotifier();
        var beforeId = Guid.CreateVersion7();
        var afterId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();

        await notifier.NotifyAsync(beforeId, snapshotId, TestCancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var received = new List<(Guid ProjectId, Guid SnapshotId)>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts.Token))
            {
                received.Add(item);
                await cts.CancelAsync();
            }
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.NotifyAsync(afterId, snapshotId, TestCancellationToken);

        await IgnoreOperationCanceledException(subscriberTask);

        // Assert
        received.ShouldHaveSingleItem();
        received[0].ProjectId.ShouldBe(afterId);
    }

    [Fact]
    public async Task DisposeAsync_CompletesAllSubscriberEnumerations()
    {
        // Arrange
        var notifier = new InProcessChangeNotifier();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var completed = false;

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var _ in notifier.SubscribeAsync(cts.Token))
            {
            }

            completed = true;
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.DisposeAsync();

        await subscriberTask;

        // Assert
        completed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisconnectedSubscriber_DoesNotAffectOtherSubscribers()
    {
        // Arrange
        await using var notifier = new InProcessChangeNotifier();
        var projectId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();

        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);

        var received2 = new List<(Guid ProjectId, Guid SnapshotId)>();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var _ in notifier.SubscribeAsync(cts1.Token))
            {
            }
        }, TestCancellationToken);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts2.Token))
            {
                received2.Add(item);
                await cts2.CancelAsync();
            }
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Disconnect subscriber 1
        await cts1.CancelAsync();
        await IgnoreOperationCanceledException(sub1);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.NotifyAsync(projectId, snapshotId, TestCancellationToken);

        await IgnoreOperationCanceledException(sub2);

        // Assert
        received2.ShouldHaveSingleItem();
        received2[0].ShouldBe((projectId, snapshotId));
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