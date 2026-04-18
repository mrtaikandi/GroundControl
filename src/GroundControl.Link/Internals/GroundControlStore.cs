using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Internals;

/// <summary>
/// Pure state container shared between the configuration provider and background services.
/// Holds configuration data, health status, and options. Raises <see cref="OnDataChanged"/>
/// when the snapshot is swapped by the background service.
/// </summary>
internal sealed class GroundControlStore
{
    private volatile StoreSnapshot _snapshot = StoreSnapshot.Empty;

    public GroundControlStore(GroundControlOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the SDK options (read-only, set at construction).
    /// </summary>
    public GroundControlOptions Options { get; }

    /// <summary>
    /// Gets the current health status.
    /// </summary>
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// Gets the timestamp of the last successful update.
    /// </summary>
    public DateTimeOffset? LastSuccessfulUpdate { get; private set; }

    /// <summary>
    /// Raised when the data snapshot is swapped.
    /// </summary>
    public event Action? OnDataChanged;

    /// <summary>
    /// Gets the current immutable snapshot.
    /// </summary>
    public StoreSnapshot GetSnapshot() => _snapshot;

    /// <summary>
    /// Atomically swaps the snapshot, marks healthy, and raises <see cref="OnDataChanged"/>.
    /// </summary>
    public void Update(Dictionary<string, ConfigValue> data, string? etag, string? lastEventId)
    {
        _snapshot = new StoreSnapshot
        {
            Data = data,
            ETag = etag,
            LastEventId = lastEventId,
            Timestamp = DateTimeOffset.UtcNow
        };

        LastSuccessfulUpdate = _snapshot.Timestamp;
        HealthStatus = HealthStatus.Healthy;
        LastErrorReason = null;
        LastError = null;
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Gets the reason for the current non-healthy status.
    /// </summary>
    public string? LastErrorReason { get; private set; }

    /// <summary>
    /// Gets the last exception that caused a non-healthy status.
    /// </summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Explicitly sets the health status without changing data.
    /// </summary>
    public void SetHealth(HealthStatus status, string? reason = null, Exception? error = null)
    {
        HealthStatus = status;
        LastErrorReason = reason;
        LastError = error;
    }
}

/// <summary>
/// Immutable snapshot of configuration data and metadata.
/// </summary>
internal sealed record StoreSnapshot
{
    public static readonly StoreSnapshot Empty = new() { Data = [] };

    public required Dictionary<string, ConfigValue> Data { get; init; }

    public string? ETag { get; init; }

    public string? LastEventId { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}