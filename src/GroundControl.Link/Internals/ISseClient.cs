namespace GroundControl.Link.Internals;

/// <summary>
/// Abstraction for a Server-Sent Events client that streams configuration events
/// from the GroundControl server.
/// </summary>
public interface ISseClient
{
    /// <summary>
    /// Opens an SSE connection and streams events from the server.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>An async enumerable of SSE events.</returns>
    IAsyncEnumerable<SseEvent> StreamAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a Server-Sent Event received from the GroundControl server.
/// </summary>
public sealed record SseEvent
{
    /// <summary>
    /// Gets the event type (e.g., "config", "heartbeat").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets the event data payload.
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// Gets the event ID, typically the snapshot version.
    /// </summary>
    public string? Id { get; init; }
}