namespace GroundControl.Link;

/// <summary>
/// Abstraction for fetching configuration from the GroundControl REST endpoint.
/// </summary>
public interface IConfigFetcher
{
    /// <summary>
    /// Fetches the current configuration from the server.
    /// </summary>
    /// <param name="etag">The ETag from a previous fetch for conditional requests, or <c>null</c> for an unconditional fetch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The fetch result, or <c>null</c> if the fetch failed.</returns>
    Task<FetchResult?> FetchAsync(string? etag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a configuration fetch from the REST endpoint.
/// </summary>
public sealed record FetchResult
{
    /// <summary>
    /// Gets the configuration entries as key-value pairs.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Config { get; init; }

    /// <summary>
    /// Gets the ETag for conditional requests.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server returned 304 Not Modified.
    /// </summary>
    public required bool NotModified { get; init; }
}