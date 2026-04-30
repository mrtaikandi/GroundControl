using GroundControl.Api.Shared.Activity;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Activity;

public sealed class LiveActivityTrackerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SubscribeAsync_WhenClientConnects_PublishesUpdatedClientCount()
    {
        // Arrange
        await using var tracker = new LiveActivityTracker();
        await using var subscription = tracker.SubscribeAsync(TestCancellationToken).GetAsyncEnumerator(TestCancellationToken);

        // Act
        var hasInitial = await subscription.MoveNextAsync();
        tracker.ClientConnected();
        var hasUpdate = await subscription.MoveNextAsync();

        // Assert
        hasInitial.ShouldBeTrue();
        hasUpdate.ShouldBeTrue();
        subscription.Current.Kind.ShouldBe(LiveActivityEventKind.Activity);
        subscription.Current.Activity.ShouldNotBeNull();
        subscription.Current.Activity.Clients.ShouldBe(1);
    }

    [Fact]
    public async Task Current_WhenClientDisconnects_ReturnsZeroClientCount()
    {
        // Arrange
        await using var tracker = new LiveActivityTracker();
        tracker.ClientConnected();

        // Act
        tracker.ClientDisconnected();

        // Assert
        tracker.Current.Clients.ShouldBe(0);
    }

    [Fact]
    public async Task SubscribeAsync_WhenAuditRecordRecorded_PublishesAuditRecordEvent()
    {
        // Arrange
        await using var tracker = new LiveActivityTracker();
        await using var subscription = tracker.SubscribeAsync(TestCancellationToken).GetAsyncEnumerator(TestCancellationToken);
        await subscription.MoveNextAsync();
        var record = new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            Action = "Created",
            EntityId = Guid.CreateVersion7(),
            EntityType = "Project",
            PerformedAt = DateTimeOffset.UtcNow,
            PerformedBy = Guid.CreateVersion7(),
        };

        // Act
        tracker.RecordAuditRecord(record);
        var hasActivityUpdate = await subscription.MoveNextAsync();

        // Assert
        hasActivityUpdate.ShouldBeTrue();
        subscription.Current.Kind.ShouldBe(LiveActivityEventKind.Activity);

        var hasAuditUpdate = await subscription.MoveNextAsync();

        hasAuditUpdate.ShouldBeTrue();
        subscription.Current.Kind.ShouldBe(LiveActivityEventKind.AuditRecord);
        subscription.Current.AuditRecord.ShouldBe(record);
    }
}