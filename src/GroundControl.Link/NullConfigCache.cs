namespace GroundControl.Link;

/// <summary>
/// A no-op cache implementation used when local caching is disabled.
/// </summary>
internal sealed class NullConfigCache : IConfigCache
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static NullConfigCache Instance { get; } = new();

    /// <inheritdoc />
    public Task<CachedConfiguration?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<CachedConfiguration?>(null);

    /// <inheritdoc />
    public Task SaveAsync(CachedConfiguration config, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}