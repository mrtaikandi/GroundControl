using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Api.Shared.Health;

/// <summary>
/// Health check stub for the change notifier subsystem.
/// </summary>
internal sealed class ChangeNotifierHealthCheck : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Change notifier is active"));
    }
}