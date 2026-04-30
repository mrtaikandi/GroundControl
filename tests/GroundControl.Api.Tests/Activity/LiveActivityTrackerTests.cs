using GroundControl.Api.Shared.Activity;
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
        subscription.Current.Clients.ShouldBe(1);
        hasUpdate.ShouldBeTrue();
        subscription.Current.Clients.ShouldBe(1);
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
}