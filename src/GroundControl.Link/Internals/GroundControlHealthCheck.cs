using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Internals;

/// <summary>
/// Health check that reports the status of the GroundControl configuration store.
/// </summary>
internal sealed class GroundControlHealthCheck : IHealthCheck
{
    private readonly GroundControlStore _store;

    public GroundControlHealthCheck(GroundControlStore store)
    {
        _store = store;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _store.HealthStatus switch
        {
            HealthStatus.Healthy => HealthCheckResult.Healthy(
                "GroundControl configuration is up to date.",
                new Dictionary<string, object>
                {
                    ["lastUpdate"] = _store.LastSuccessfulUpdate?.ToString("O") ?? "never",
                    ["connectionMode"] = _store.Options.ConnectionMode.ToString(),
                    ["etag"] = _store.GetSnapshot().ETag ?? "none"
                }),

            HealthStatus.Degraded => HealthCheckResult.Degraded(
                _store.LastErrorReason ?? "GroundControl server unreachable. Serving from cache.",
                _store.LastError),

            _ => HealthCheckResult.Unhealthy(
                _store.LastErrorReason ?? "No GroundControl configuration data available.",
                _store.LastError)
        };

        return Task.FromResult(result);
    }
}