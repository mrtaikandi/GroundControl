namespace GroundControl.Link.Internals;

/// <summary>
/// Abstraction for a Server-Sent Events client that streams configuration events
/// from the GroundControl server.
/// </summary>
public interface IGroundControlSseClient
{
    /// <summary>
    /// Gets or sets the last received SSE event ID, used to resume streams after reconnection.
    /// </summary>
    /// <remarks>
    /// Set this before calling <see cref="StreamAsync"/> to resume from a previously known event ID
    /// (e.g., restored from cache after a process restart).
    /// </remarks>
    string? LastEventId { get; set; }

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