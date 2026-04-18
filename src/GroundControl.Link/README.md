# GroundControl.Link

.NET client SDK for [GroundControl](https://github.com/mrtaikandi/GroundControl) — plugs directly into `Microsoft.Extensions.Configuration` so your app receives real-time configuration updates with zero custom plumbing.

## Installation

```bash
dotnet add package GroundControl.Link
```

## Quick Start

Integration is a two-step process: register the configuration source, then register the background services.

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Add GroundControl as a configuration source
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl    = new Uri("https://groundcontrol.example.com");
    options.ClientId     = "<client-id>";
    options.ClientSecret = "<client-secret>";
});

// 2. Register background services (connection strategy, health check, metrics)
builder.Services.AddGroundControl(builder.Configuration);

var app = builder.Build();
```

Configuration is available immediately after step 1 completes (the SDK fetches the active snapshot synchronously during startup). Step 2 keeps the configuration synchronized in the background via SSE or polling.

Values are accessed through the standard `IConfiguration` API:

```csharp
var connectionString = app.Configuration["Database:ConnectionString"];
```

## Connection Modes

The `ConnectionMode` option controls how the SDK stays synchronized after startup:

| Mode | Behavior |
|------|----------|
| `SseWithPollingFallback` | **(default)** Opens an SSE stream for real-time updates. Falls back to REST polling if SSE fails, and periodically retries SSE in the background. |
| `Sse` | SSE-only with exponential backoff retry on disconnect. |
| `Polling` | Periodic REST polling at a configurable interval (default 5 minutes) with jitter. |
| `StartupOnly` | Fetches configuration once at startup. No background service is registered. |

```csharp
builder.Configuration.AddGroundControl(options =>
{
    // ...
    options.ConnectionMode = ConnectionMode.Polling;
    options.PollingInterval = TimeSpan.FromMinutes(2);
});
```

## Local Caching

When `EnableLocalCache` is `true` (the default), the SDK writes the active snapshot to a local JSON file. On the next startup, if the server is unreachable, the SDK serves configuration from the cache so the application can still start.

```csharp
options.EnableLocalCache = true;
options.CacheFilePath    = "./groundcontrol-cache.json"; // default
```

Cached values are written in plaintext by default. To encrypt sensitive values at rest, implement `IConfigurationProtector` and assign it to `options.Protector`:

```csharp
options.Protector = new MyProtector(); // implements IConfigurationProtector
```

Only entries the server has marked as sensitive are passed through the protector; non-sensitive entries (feature flags, URLs, thresholds) remain inspectable as plaintext in the cache file. The SDK treats the ciphertext returned by `Protect` as opaque — key rotation and algorithm versioning are the implementation's responsibility. If `Unprotect` throws (tampering, wrong key, unrecognized format), the cache file is invalidated and the SDK refetches from the server. The same happens when the cache was written under a different protector configuration than the current one.

## Health Checks

The SDK registers a health check named `GroundControl`:

| Status | Meaning |
|--------|---------|
| **Healthy** | Connected to the server and serving the latest snapshot. |
| **Degraded** | Server unreachable but serving from cache. |
| **Unhealthy** | No configuration available (no cache, no server). |

The health check is tagged with `ready` by default and can be customized via `GroundControlOptions.HealthCheckTags`.

## Metrics

The SDK emits OpenTelemetry metrics under the `GroundControl.Link` meter:

| Instrument | Type | Description |
|------------|------|-------------|
| `groundcontrol.link.fetch.count` | Counter | REST fetch attempts (tagged by status) |
| `groundcontrol.link.fetch.duration` | Histogram | REST fetch latency (seconds) |
| `groundcontrol.link.reload.count` | Counter | Configuration reloads (tagged by source: `sse`, `polling`) |
| `groundcontrol.link.sse.reconnect.count` | Counter | SSE reconnection attempts |
| `groundcontrol.link.sse.connected` | UpDownCounter | 1 when SSE is connected, 0 when disconnected |

## Options Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServerUrl` | `Uri` | *required* | GroundControl server address |
| `ClientId` | `string` | *required* | Client credential ID |
| `ClientSecret` | `string` | *required* | Client credential secret |
| `ConnectionMode` | `ConnectionMode` | `SseWithPollingFallback` | How the SDK stays synchronized |
| `StartupTimeout` | `TimeSpan` | 10 s | Maximum wait for the server during startup |
| `PollingInterval` | `TimeSpan` | 5 min | REST polling frequency |
| `SseReconnectDelay` | `TimeSpan` | 5 s | Base delay for SSE reconnection (exponential backoff) |
| `SseMaxReconnectDelay` | `TimeSpan` | 5 min | Cap on SSE reconnection delay |
| `SseHeartbeatTimeout` | `TimeSpan` | 2 min | SSE stream idle timeout |
| `EnableLocalCache` | `bool` | `true` | Enable file-based caching |
| `CacheFilePath` | `string` | `./groundcontrol-cache.json` | Cache file path |
| `Protector` | `IConfigurationProtector?` | `null` | Optional cipher for cache values; `null` means plaintext cache |
| `ApiVersion` | `string` | `1.0` | API version header value |
| `HealthCheckTags` | `IList<string>` | `["ready"]` | Health check tags |

## Documentation

See the full [SDK documentation](https://github.com/mrtaikandi/GroundControl/blob/main/docs/guide/sdk/quickstart.md) for detailed guides on connection modes, caching, and configuration patterns.
