# SDK Options Reference

These properties are set on the options object passed to `AddGroundControl()`.
Required properties must be set before the SDK will start.

| Property | Type | Default | Description |
|---|---|---|---|
| `ServerUrl` | `string` | _(required)_ | Base URL of the GroundControl server. |
| `ClientId` | `string` | _(required)_ | Client ID for authentication. Obtain from the server when creating a client. |
| `ClientSecret` | `string` | _(required)_ | Client secret for authentication. Shown only once at client creation. |
| `ConnectionMode` | `ConnectionMode` | `SseWithPollingFallback` | How the SDK connects to the server. See [Connection Modes](connection-modes.md). |
| `StartupTimeout` | `TimeSpan` | `10 seconds` | Maximum time to wait for the server during initial connection. If the server doesn't respond in time, the SDK falls back to the local cache. |
| `PollingInterval` | `TimeSpan` | `5 minutes` | How often to poll the server for updates when using polling mode. |
| `SseReconnectDelay` | `TimeSpan` | `5 seconds` | Base delay before reconnecting after an SSE disconnect. Increases exponentially on repeated failures. |
| `SseMaxReconnectDelay` | `TimeSpan` | `5 minutes` | Maximum delay between SSE reconnection attempts. |
| `SseHeartbeatTimeout` | `TimeSpan` | `2 minutes` | If no data (including heartbeats) is received for this duration, the SSE connection is considered lost. |
| `EnableLocalCache` | `bool` | `true` | Whether to persist configuration to a local file for offline resilience. |
| `CacheFilePath` | `string` | `./groundcontrol-cache.json` | Path to the local cache file. The directory must exist. |
| `ApiVersion` | `string` | `1.0` | API version sent in the `api-version` header. |
| `LoggerFactory` | `ILoggerFactory?` | `null` | Logger factory for SDK diagnostic output. If not set, the SDK does not log. |

## Example with all options

```csharp
builder.Configuration.AddGroundControl(options =>
{
    // Required
    options.ServerUrl = "https://groundcontrol.example.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";

    // Connection
    options.ConnectionMode = ConnectionMode.SseWithPollingFallback;
    options.StartupTimeout = TimeSpan.FromSeconds(15);
    options.PollingInterval = TimeSpan.FromMinutes(2);

    // SSE tuning
    options.SseReconnectDelay = TimeSpan.FromSeconds(3);
    options.SseMaxReconnectDelay = TimeSpan.FromMinutes(2);
    options.SseHeartbeatTimeout = TimeSpan.FromMinutes(1);

    // Caching
    options.EnableLocalCache = true;
    options.CacheFilePath = "/var/cache/groundcontrol.json";

    // Diagnostics
    options.LoggerFactory = loggerFactory;
});
```

See also: [SDK Quickstart](quickstart.md) | [Connection Modes](connection-modes.md)
