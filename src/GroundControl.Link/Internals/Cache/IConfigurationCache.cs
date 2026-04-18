namespace GroundControl.Link.Internals.Cache;

/// <summary>
/// Abstraction for local caching of configuration data.
/// </summary>
internal interface IConfigurationCache : IDisposable
{
    /// <summary>
    /// Loads cached configuration synchronously. Used during startup.
    /// </summary>
    CachedConfiguration? Load();

    /// <summary>
    /// Saves configuration synchronously. Used during startup.
    /// </summary>
    void Save(CachedConfiguration config);

    /// <summary>
    /// Loads cached configuration from the local store.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The cached configuration, or <c>null</c> if no cache is available.</returns>
    Task<CachedConfiguration?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration to the local store.
    /// </summary>
    /// <param name="config">The configuration to cache.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveAsync(CachedConfiguration config, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents cached configuration data with optional snapshot metadata.
/// </summary>
internal sealed record CachedConfiguration
{
    /// <summary>
    /// Gets the configuration entries keyed by flattened path, each carrying a value and a sensitivity flag.
    /// </summary>
    public required IReadOnlyDictionary<string, ConfigValue> Entries { get; init; }

    /// <summary>
    /// Gets the REST ETag for conditional requests.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Gets the last SSE event ID for resuming streams across restarts.
    /// </summary>
    public string? LastEventId { get; init; }
}