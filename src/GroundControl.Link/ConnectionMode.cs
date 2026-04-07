namespace GroundControl.Link;

/// <summary>
/// Specifies how the SDK connects to the GroundControl server.
/// </summary>
public enum ConnectionMode
{
    /// <summary>
    /// Uses SSE with automatic fallback to REST polling if SSE fails.
    /// </summary>
    SseWithPollingFallback,

    /// <summary>
    /// Uses SSE exclusively with exponential backoff retry on disconnect.
    /// </summary>
    Sse,

    /// <summary>
    /// Uses periodic REST polling only.
    /// </summary>
    Polling,

    /// <summary>
    /// Fetches configuration only during startup (Phase 1). No background service is registered.
    /// </summary>
    OnlyOnStartup
}