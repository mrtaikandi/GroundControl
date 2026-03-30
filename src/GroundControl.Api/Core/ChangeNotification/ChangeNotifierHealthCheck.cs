using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Api.Core.ChangeNotification;

/// <summary>
/// Health check for the change notifier subsystem.
/// </summary>
internal sealed class ChangeNotifierHealthCheck : IHealthCheck
{
    private readonly IChangeNotifier _notifier;

    public ChangeNotifierHealthCheck(IChangeNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var (isHealthy, description) = _notifier switch
        {
            MongoChangeStreamNotifier { IsDisposed: true } => (false, "Change notifier is disposed"),
            MongoChangeStreamNotifier { IsConnected: false } => (false, "Change stream is not connected"),
            MongoChangeStreamNotifier => (true, "Change stream is connected"),
            InProcessChangeNotifier { IsDisposed: true } => (false, "Change notifier is disposed"),
            InProcessChangeNotifier => (true, "Change notifier is active"),
            _ => (true, "Change notifier is active")
        };

        var result = isHealthy
            ? HealthCheckResult.Healthy(description)
            : HealthCheckResult.Unhealthy(description);

        return Task.FromResult(result);
    }
}