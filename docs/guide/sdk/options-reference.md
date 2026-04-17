# SDK Options Reference

These properties are set on the options object passed to `AddGroundControl()` on `IConfigurationBuilder` (Phase 1).
Required properties must be set before the SDK will start.

| Property | Type | Default | Description |
|---|---|---|---|
| `ServerUrl` | `Uri` | _(required)_ | Base URL of the GroundControl server. |
| `ClientId` | `string` | _(required)_ | Client ID for authentication. Obtain from the server when creating a client. |
| `ClientSecret` | `string` | _(required)_ | Client secret for authentication. Shown only once at client creation. |
| `ConnectionMode` | `ConnectionMode` | `SseWithPollingFallback` | How the SDK connects to the server. See [Connection Modes](connection-modes.md). |
| `StartupTimeout` | `TimeSpan` | `10 seconds` | Maximum time to wait for the server during initial connection. If the server doesn't respond in time, the SDK falls back to the local cache. |
| `PollingInterval` | `TimeSpan` | `5 minutes` | How often to poll the server for updates when using polling mode. |
| `SseReconnectDelay` | `TimeSpan` | `5 seconds` | Base delay before reconnecting after an SSE disconnect. Increases exponentially on repeated failures. |
| `SseMaxReconnectDelay` | `TimeSpan` | `5 minutes` | Maximum delay between SSE reconnection attempts. Must be >= `SseReconnectDelay`. |
| `SseHeartbeatTimeout` | `TimeSpan` | `2 minutes` | If no data (including heartbeats) is received for this duration, the SSE connection is considered lost. |
| `EnableLocalCache` | `bool` | `true` | Whether to persist configuration to a local file for offline resilience. |
| `CacheFilePath` | `string` | `./groundcontrol-cache.json` | Path to the local cache file. The directory must exist. |
| `ApiVersion` | `string` | `1.0` | API version sent in the `api-version` header. |
| `HealthCheckTags` | `IList<string>` | `["ready"]` | Tags applied when registering the GroundControl health check. |
| `Scopes` | `Dictionary<string, string>` | _(empty)_ | Scope dimensions sent to the server on every request via the `GroundControl-Scopes` header. Merged with the scopes bound to the Client entity on the server; on key conflict, the server-defined scopes take priority. Uses case-insensitive key comparison. |

All `TimeSpan` properties must be positive. Validation runs at startup and throws `OptionsValidationException` on failure.

## Example with all options

```csharp
var builder = WebApplication.CreateBuilder(args);

// Phase 1: Configuration provider
builder.Configuration.AddGroundControl(options =>
{
    // Required
    options.ServerUrl = new Uri("https://groundcontrol.example.com");
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
    options.ApiVersion = "1.0";

    // SDK-provided scopes (merged with server-defined Client scopes)
    options.Scopes["Environment"] = "prod";
    options.Scopes["Region"] = "eu-west";
});

// Phase 2: Background services, health checks, metrics
builder.Services.AddGroundControl(builder.Configuration);

var app = builder.Build();
```

## Phase 2 parameters

The `AddGroundControl` call on `IServiceCollection` accepts two optional parameters:

| Parameter | Type | Description |
|---|---|---|
| `configureHttpClient` | `Action<IHttpClientBuilder>?` | Customize the `HttpClient` used for server communication (e.g. add resilience handlers or delegating handlers). |
| `configureOptions` | `Action<GroundControlOptions>?` | Apply additional option changes after the provider is created. Useful for settings that only affect background services. |

```csharp
builder.Services.AddGroundControl(
    builder.Configuration,
    configureHttpClient: httpBuilder =>
    {
        httpBuilder.AddStandardResilienceHandler();
    },
    configureOptions: options =>
    {
        options.HealthCheckTags.Add("groundcontrol");
    });
```

See also: [SDK Quickstart](quickstart.md) | [Connection Modes](connection-modes.md)