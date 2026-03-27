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
    public Task<IReadOnlyDictionary<string, string>?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>?>(null);

    /// <inheritdoc />
    public Task SaveAsync(IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}