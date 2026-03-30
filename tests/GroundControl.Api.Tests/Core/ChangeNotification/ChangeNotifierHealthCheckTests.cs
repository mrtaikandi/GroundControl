using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Persistence.MongoDb;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.ChangeNotification;

public sealed class ChangeNotifierHealthCheckTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckHealthAsync_InProcess_WhenActive_ReturnsHealthy()
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
    public async Task CheckHealthAsync_InProcess_WhenDisposed_ReturnsUnhealthy()
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

    [Fact]
    public async Task CheckHealthAsync_MongoChangeStream_WhenNotStarted_ReturnsUnhealthy()
    {
        // Arrange
        var context = Substitute.For<IMongoDbContext>();
        var notifier = new MongoChangeStreamNotifier(context, NullLogger<MongoChangeStreamNotifier>.Instance);
        var healthCheck = new ChangeNotifierHealthCheck(notifier);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not connected");

        await notifier.DisposeAsync();
    }

    [Fact]
    public async Task CheckHealthAsync_MongoChangeStream_WhenDisposed_ReturnsUnhealthy()
    {
        // Arrange
        var context = Substitute.For<IMongoDbContext>();
        var notifier = new MongoChangeStreamNotifier(context, NullLogger<MongoChangeStreamNotifier>.Instance);
        await notifier.DisposeAsync();
        var healthCheck = new ChangeNotifierHealthCheck(notifier);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestCancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("disposed");
    }
}