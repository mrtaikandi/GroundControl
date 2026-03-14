using GroundControl.Api.Shared.Notification;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Api.Shared.Health;

/// <summary>
/// Health check for the change notifier subsystem.
/// </summary>
internal sealed class ChangeNotifierHealthCheck : IHealthCheck
{
    private readonly InProcessChangeNotifier _notifier;

    public ChangeNotifierHealthCheck(IChangeNotifier notifier)
    {
        _notifier = (InProcessChangeNotifier)notifier;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = _notifier.IsDisposed
            ? HealthCheckResult.Unhealthy("Change notifier is disposed")
            : HealthCheckResult.Healthy("Change notifier is active");

        return Task.FromResult(result);
    }
}