using GroundControl.Api.Shared.Health;
using GroundControl.Api.Shared.Notification;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Health;

public sealed class ChangeNotifierHealthCheckTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckHealthAsync_WhenActive_ReturnsHealthy()
    {
        // Arrange
        await using var notifier = new InProcessChangeNotifier();
        var healthCheck = new ChangeNotifierHealthCheck(notifier);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisposed_ReturnsUnhealthy()
    {
        // Arrange
        var notifier = new InProcessChangeNotifier();
        await notifier.DisposeAsync();
        var healthCheck = new ChangeNotifierHealthCheck(notifier);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}