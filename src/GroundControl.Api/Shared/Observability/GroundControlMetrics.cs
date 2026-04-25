using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GroundControl.Api.Shared.Observability;

/// <summary>
/// Centralized OpenTelemetry metrics and activity source for GroundControl.
/// </summary>
internal static class GroundControlMetrics
{
    /// <summary>
    /// Gets the activity source name used for all GroundControl custom traces.
    /// </summary>
    public const string ActivitySourceName = "GroundControl";

    /// <summary>
    /// Gets the meter name used for all GroundControl custom metrics.
    /// </summary>
    public const string MeterName = "GroundControl";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Gets the activity source for creating custom trace spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Gets the counter tracking snapshot cache hits.
    /// </summary>
    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>(
            "groundcontrol.cache.hits",
            description: "Number of snapshot cache hits");

    /// <summary>
    /// Gets the counter tracking snapshot cache misses.
    /// </summary>
    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>(
            "groundcontrol.cache.misses",
            description: "Number of snapshot cache misses");

    /// <summary>
    /// Gets the counter tracking change notifier events dispatched.
    /// </summary>
    public static readonly Counter<long> ChangeNotifierEvents =
        Meter.CreateCounter<long>(
            "groundcontrol.changenotifier.events",
            description: "Total number of change notifier events dispatched");

    /// <summary>
    /// Gets the counter tracking the total number of snapshots activated.
    /// </summary>
    public static readonly Counter<long> SnapshotsActivated =
        Meter.CreateCounter<long>(
            "groundcontrol.snapshots.activated.total",
            description: "Total number of snapshots activated");

    /// <summary>
    /// Gets the counter tracking the total number of snapshots published.
    /// </summary>
    public static readonly Counter<long> SnapshotsPublished =
        Meter.CreateCounter<long>(
            "groundcontrol.snapshots.published.total",
            description: "Total number of snapshots published");

    /// <summary>
    /// Gets the gauge tracking the number of active SSE connections.
    /// </summary>
    public static readonly UpDownCounter<long> SseActiveConnections =
        Meter.CreateUpDownCounter<long>(
            "groundcontrol.sse.connections.active",
            description: "Number of active SSE connections");

    /// <summary>
    /// Gets the counter tracking the total number of SSE connections established.
    /// </summary>
    public static readonly Counter<long> SseTotalConnections =
        Meter.CreateCounter<long>(
            "groundcontrol.sse.connections.total",
            description: "Total number of SSE connections established");
}