namespace GroundControl.Link.Internals;

/// <summary>
/// Pure state container bridging Phase 1 (pre-DI) and Phase 2 (post-DI).
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
    public StoreHealthStatus HealthStatus { get; private set; } = StoreHealthStatus.Unhealthy;

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
    public void Update(Dictionary<string, string> data, string? etag, string? lastEventId)
    {
        _snapshot = new StoreSnapshot
        {
            Data = data,
            ETag = etag,
            LastEventId = lastEventId,
            Timestamp = DateTimeOffset.UtcNow
        };

        LastSuccessfulUpdate = _snapshot.Timestamp;
        HealthStatus = StoreHealthStatus.Healthy;
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Explicitly sets the health status without changing data.
    /// </summary>
    public void SetHealth(StoreHealthStatus status) => HealthStatus = status;
}

/// <summary>
/// Immutable snapshot of configuration data and metadata.
/// </summary>
internal sealed record StoreSnapshot
{
    public static readonly StoreSnapshot Empty = new()
    {
        Data = new Dictionary<string, string>()
    };

    public required Dictionary<string, string> Data { get; init; }

    public string? ETag { get; init; }

    public string? LastEventId { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Health status of the GroundControl store.
/// </summary>
internal enum StoreHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}