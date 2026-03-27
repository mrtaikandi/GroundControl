using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Link;

/// <summary>
/// Configuration options for the GroundControl client SDK.
/// </summary>
public sealed class GroundControlOptions
{
    /// <summary>
    /// Gets or sets the GroundControl server URL.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URL is the design contract for consumer convenience")]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID for authentication.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for authentication.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection mode.
    /// </summary>
    /// <remarks>Defaults to <see cref="ConnectionMode.SseWithPollingFallback"/>.</remarks>
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.SseWithPollingFallback;

    /// <summary>
    /// Gets or sets the maximum time to wait for the server on startup.
    /// </summary>
    /// <remarks>Defaults to 10 seconds.</remarks>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the polling interval.
    /// </summary>
    /// <remarks>Defaults to 5 minutes.</remarks>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the base delay for SSE reconnection with exponential backoff.
    /// </summary>
    /// <remarks>Defaults to 5 seconds.</remarks>
    public TimeSpan SseReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum delay between SSE reconnection attempts.
    /// </summary>
    /// <remarks>Defaults to 5 minutes.</remarks>
    public TimeSpan SseMaxReconnectDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the heartbeat timeout for SSE connections.
    /// </summary>
    /// <remarks>Defaults to 2 minutes.</remarks>
    public TimeSpan SseHeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets a value indicating whether local file caching is enabled.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool EnableLocalCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the path for the local cache file.
    /// </summary>
    /// <remarks>Defaults to <c>./groundcontrol-cache.json</c>.</remarks>
    public string CacheFilePath { get; set; } = "./groundcontrol-cache.json";

    /// <summary>
    /// Gets or sets the API version sent in the <c>api-version</c> header.
    /// </summary>
    /// <remarks>Defaults to <c>1.0</c>.</remarks>
    public string ApiVersion { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the logger factory for SDK diagnostics.
    /// </summary>
    /// <remarks>If not set, logging is disabled.</remarks>
    public ILoggerFactory? LoggerFactory { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            throw new ArgumentException("ServerUrl is required.", nameof(ServerUrl));
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new ArgumentException("ClientId is required.", nameof(ClientId));
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new ArgumentException("ClientSecret is required.", nameof(ClientSecret));
        }
    }
}