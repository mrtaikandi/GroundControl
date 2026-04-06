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
    /// <returns>The fetch result containing the status and optional configuration data.</returns>
    Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Indicates the outcome of a configuration fetch from the REST endpoint.
/// </summary>
public enum FetchStatus
{
    /// <summary>
    /// Configuration was fetched successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The server returned 304 Not Modified.
    /// </summary>
    NotModified,

    /// <summary>
    /// A transient error occurred (network, 5xx, etc.) — safe to retry.
    /// </summary>
    TransientError,

    /// <summary>
    /// Authentication failed (401/403) — retrying will not help.
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// The requested configuration was not found (404).
    /// </summary>
    NotFound
}

/// <summary>
/// Represents the result of a configuration fetch from the REST endpoint.
/// </summary>
public sealed record FetchResult
{
    /// <summary>
    /// Gets the fetch outcome status.
    /// </summary>
    public required FetchStatus Status { get; init; }

    /// <summary>
    /// Gets the configuration entries as key-value pairs, or <c>null</c> on non-success status.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Config { get; init; }

    /// <summary>
    /// Gets the ETag for conditional requests.
    /// </summary>
    public string? ETag { get; init; }
}