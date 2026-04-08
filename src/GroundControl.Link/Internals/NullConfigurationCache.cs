namespace GroundControl.Link.Internals;

/// <summary>
/// A no-op cache implementation used when local caching is disabled.
/// </summary>
internal sealed class NullConfigurationCache : IConfigurationCache
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static NullConfigurationCache Instance { get; } = new();

    /// <inheritdoc />
    public CachedConfiguration? Load() => null;

    /// <inheritdoc />
    public void Save(CachedConfiguration config)
    {
    }

    /// <inheritdoc />
    public Task<CachedConfiguration?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<CachedConfiguration?>(null);

    /// <inheritdoc />
    public Task SaveAsync(CachedConfiguration config, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Dispose()
    {
    }
}