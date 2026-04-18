namespace GroundControl.Link.Internals.Client;

/// <summary>
/// Client for the GroundControl API, providing both REST config fetch and SSE stream access.
/// </summary>
internal interface IGroundControlApiClient
{
    /// <summary>
    /// Fetches the current configuration from the server with ETag-based conditional requests.
    /// </summary>
    /// <param name="etag">The ETag from a previous fetch for conditional requests, or <c>null</c> for an unconditional fetch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The fetch result containing the status and optional configuration data.</returns>
    Task<FetchResult> FetchConfigAsync(string? etag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a streaming SSE connection to the configuration stream endpoint.
    /// </summary>
    /// <param name="lastEventId">An optional Last-Event-ID for resuming from a prior position.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The HTTP response message with headers-only completion. The caller owns and must dispose this response.</returns>
    Task<HttpResponseMessage> GetConfigStreamAsync(string? lastEventId, CancellationToken cancellationToken);
}

/// <summary>
/// Indicates the outcome of a configuration fetch from the REST endpoint.
/// </summary>
internal enum FetchStatus
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
internal sealed record FetchResult
{
    /// <summary>
    /// Gets the fetch outcome status.
    /// </summary>
    public required FetchStatus Status { get; init; }

    /// <summary>
    /// Gets the configuration entries keyed by flattened path, or <c>null</c> on non-success status. Each entry carries the value and a sensitivity flag.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigValue>? Config { get; init; }

    /// <summary>
    /// Gets the ETag for conditional requests.
    /// </summary>
    public string? ETag { get; init; }
}