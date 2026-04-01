namespace GroundControl.Link;

/// <summary>
/// Abstraction for local caching of configuration data.
/// </summary>
public interface IConfigCache
{
    /// <summary>
    /// Loads cached configuration from the local store.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The cached configuration, or <c>null</c> if no cache is available.</returns>
    Task<IReadOnlyDictionary<string, string>?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration to the local store.
    /// </summary>
    /// <param name="config">The configuration entries to cache.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveAsync(IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken = default);
}