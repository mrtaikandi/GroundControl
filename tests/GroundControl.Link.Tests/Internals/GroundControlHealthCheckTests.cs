using GroundControl.Link.Internals;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Tests.Internals;

public sealed class GroundControlHealthCheckTests
{
    private readonly GroundControlStore _store = new(new GroundControlOptions
    {
        ServerUrl = "http://localhost",
        ClientId = "test",
        ClientSecret = "secret"
    });

    [Fact]
    public async Task CheckHealthAsync_Healthy_ReturnsHealthy()
    {
        // Arrange
        _store.Update(new Dictionary<string, string> { ["K"] = "V" }, "\"1\"", null);
        var check = new GroundControlHealthCheck(_store);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Degraded_ReturnsDegraded()
    {
        // Arrange
        _store.SetHealth(HealthStatus.Degraded);
        var check = new GroundControlHealthCheck(_store);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_Degraded_IncludesReasonAndException()
    {
        // Arrange
        var error = new HttpRequestException("Connection refused");
        _store.SetHealth(HealthStatus.Degraded, "Server returned a transient error.", error);
        var check = new GroundControlHealthCheck(_store);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("Server returned a transient error.");
        result.Exception.ShouldBeSameAs(error);
    }

    [Fact]
    public async Task CheckHealthAsync_Unhealthy_ReturnsUnhealthy()
    {
        // Arrange — store starts Unhealthy by default
        var check = new GroundControlHealthCheck(_store);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Unhealthy_IncludesReasonAndException()
    {
        // Arrange
        var error = new HttpRequestException("DNS resolution failed");
        _store.SetHealth(HealthStatus.Unhealthy, "Authentication failed (401/403). Check ClientId and ClientSecret.", error);
        var check = new GroundControlHealthCheck(_store);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Authentication failed (401/403). Check ClientId and ClientSecret.");
        result.Exception.ShouldBeSameAs(error);
    }
}