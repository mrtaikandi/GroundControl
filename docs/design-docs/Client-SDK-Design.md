# Client SDK Design

This document defines the design of the GroundControl .NET client SDK, which integrates with the standard `IConfiguration` system to provide real-time configuration from GroundControl.

See [API Design](API-Design.md) for the Client API endpoints and [Security Model](Security-Model.md) for authentication details.

---

## Overview

The client SDK is a NuGet package (`GroundControl.Link`) that provides an `IConfigurationProvider` implementation. It plugs into .NET's standard configuration pipeline and delivers configuration from GroundControl with automatic real-time updates.

### Key Characteristics

- Integrates with `IConfigurationBuilder` via a standard extension method.
- Provides real-time updates via SSE without application restarts.
- Implements a multi-tier fallback chain for resilience.
- Handles sensitive values transparently via optional data protection.
- Supports .NET's hierarchical key model (colon-separated keys like `Logging:LogLevel:Default`).

---

## Two-Phase Architecture

The SDK operates in two distinct phases:

### Phase 1: Startup (Configuration Provider)

Runs synchronously during `IConfigurationSource.Build()`, before the DI container is finalized. Creates a short-lived `HttpClient` for a single conditional GET to load configuration. Falls back to a local file cache if the server is unreachable.

- Registered via `IConfigurationBuilder.AddGroundControl()`
- Creates `GroundControlStore`, `IConfigurationCache`, and `IGroundControlApiClient` directly (no DI)
- Uses `NullLogger` since the logging infrastructure is not yet available

### Phase 2: Background Services

Runs after the host starts, using DI-resolved services. A `LinkBackgroundService` delegates to an `IConnectionStrategy` that maintains SSE connections and/or polls for updates. Updates flow through the shared `GroundControlStore`, which raises `OnDataChanged` to trigger the provider's `OnReload()`.

- Registered via `IServiceCollection.AddGroundControl()`
- Uses `IHttpClientFactory` for managed `HttpClient` lifecycle
- Full logging, metrics, and health checks available

---

## Integration

### Registration

Registration is a two-step process corresponding to the two phases:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Phase 1: Configuration provider (runs during startup)
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = new Uri("https://groundcontrol.example.com");
    options.ClientId = "0192d4e0-7b3a-7f2e-8a1c-4d5e6f7a8b9c";
    options.ClientSecret = "a1b2c3d4e5f6...";

    // Optional settings
    options.CacheFilePath = "./groundcontrol-cache.json";
    options.PollingInterval = TimeSpan.FromMinutes(5);
    options.SseReconnectDelay = TimeSpan.FromSeconds(5);
    options.SseHeartbeatTimeout = TimeSpan.FromMinutes(2);
    options.ConnectionMode = ConnectionMode.SseWithPollingFallback; // default
});

// Phase 2: Background services, health checks, metrics
builder.Services.AddGroundControl(
    builder.Configuration,
    configureHttpClient: httpBuilder =>
    {
        // Optional: customize the HttpClient (e.g., add a delegating handler)
    });
```

The `IServiceCollection.AddGroundControl()` method locates the `GroundControlConfigurationProvider` from the built configuration and shares its `GroundControlStore` and `IConfigurationCache` instances with the background services. It throws `InvalidOperationException` if `IConfigurationBuilder.AddGroundControl()` was not called first.

### Configuration Precedence

The GroundControl provider is added to the configuration pipeline alongside other providers. .NET's configuration system uses last-registered-wins, so the registration order determines precedence:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")                // Lowest priority (base defaults)
    .AddJsonFile($"appsettings.{env}.json", true)   // Environment overrides
    .AddGroundControl(options => { ... })            // GroundControl overrides (highest priority from remote)
    .AddEnvironmentVariables();                      // Local env vars can still override
```

GroundControl values override `appsettings.json` values for the same keys. Environment variables (if registered after) can override GroundControl values for local testing/debugging.

---

## Connection Modes

### SSE with Polling Fallback (Default)

The recommended mode. Attempts SSE first for real-time updates, automatically falls back to polling if SSE fails. Both can run concurrently; either can update the store.

```
┌─────────────┐
│  App Starts  │
└──────┬──────┘
       │
       ▼
┌──────────────┐     Success     ┌─────────────────┐
│ Connect SSE  │────────────────→│ Receive config   │
│              │                 │ via SSE stream   │
└──────┬───────┘                 └────────┬─────────┘
       │ Failure / Disconnect             │
       ▼                                  │ Disconnected
┌──────────────┐                          │
│ Poll REST    │◄─────────────────────────┘
│ (periodic)   │
└──────┬───────┘
       │ SSE reconnect attempt (exponential backoff)
       ▼
┌──────────────┐
│ Reconnect    │──→ (back to SSE on success)
│ SSE          │
└──────────────┘
```

### SSE Only

Uses SSE exclusively. If the connection drops, the SDK retries with exponential backoff but does not fall back to polling.

### Polling Only

Periodically polls the REST endpoint at a configurable interval with jitter (75-125% of the base interval). Suitable for environments where SSE is not supported (e.g., certain proxies or firewalls). Registers `NoOpGroundControlSseClient` since SSE is unused.

### Startup Only

Fetches configuration during Phase 1 only. No background service, `HttpClient`, SSE client, or connection strategy is registered. Useful for batch jobs or short-lived processes that only need configuration at startup.

---

## Fallback Chain

When the application starts (Phase 1), the SDK attempts to load configuration in this order:

```
1. Local file cache (loaded first for ETag)
   │ Provides ETag for conditional GET
   │
2. REST conditional GET to /client/config
   │ Success → Use server config, update store and cache
   │ 304 Not Modified → Use cached config
   │ Failure ↓
   │
3. Cached config (if available)
   │ Exists → Use cached config, mark Degraded
   │ Missing → Mark Unhealthy, start with empty config
```

### Startup Behavior

| Server available | Cache exists | Behavior |
|-----------------|--------------|----------|
| Yes (200) | Any | Load from server, update cache, mark Healthy |
| Yes (304) | Yes | Load from cache (unchanged), mark Healthy |
| No | Yes | Load from cache, mark Degraded |
| No | No | Empty config, mark Unhealthy. Background service retries. |

The SDK never blocks startup indefinitely. Phase 1 performs a synchronous HTTP call with the default `HttpClient` timeout.

### Configuration Options

```csharp
options.StartupTimeout = TimeSpan.FromSeconds(10);  // Max wait for server on startup
options.CacheFilePath = "./groundcontrol-cache.json";  // Local cache file path
options.EnableLocalCache = true;                     // Enable/disable local file caching
options.ApiVersion = "1.0";                          // API version sent in api-version header
```

**API version header:** The SDK sends the `api-version` header on all REST and SSE requests. The default value is `1.0`. Override via the `ApiVersion` option if connecting to a server that requires a different version.

---

## Local File Cache

The local file cache stores the last successfully received configuration to disk, along with metadata for conditional requests and SSE resumption.

### Cache File Format

```json
{
  "eTag": "\"12\"",
  "lastEventId": "evt-abc123",
  "timestamp": "2024-01-15T10:30:00+00:00",
  "entries": {
    "Logging:LogLevel:Default": "Warning",
    "Database:ConnectionString": "***ENCRYPTED:<ciphertext>"
  }
}
```

The `entries` field contains flat key-value pairs. The `eTag` enables conditional GET requests (304 Not Modified). The `lastEventId` allows SSE streams to resume from the last known position after a process restart.

When an `IDataProtectionProvider` is supplied to the `FileConfigurationCache` constructor, all values are encrypted with a `***ENCRYPTED:` prefix. Without data protection, values are stored in plaintext. The standard `AddGroundControl(...)` extension methods do not currently pass a data protection provider, since `IConfigurationSource.Build()` runs before the DI container is finalized. Applications needing encrypted caches must wire the `IDataProtectionProvider` manually.

### Sensitive Value Protection in Cache

Sensitive values in the local cache file are encrypted using the platform's data protection mechanism:

| Platform | Protection Method |
|----------|-----------------|
| Windows | DPAPI (Data Protection API) - encrypted to the machine or user context |
| Linux/Container | ASP.NET Core Data Protection with a configured key ring |

The encryption key is derived from the machine identity, so the cache file is not portable between machines.

### Cache Update Strategy

- The cache is updated atomically (write to temp file, then rename) to prevent corruption.
- A `SemaphoreSlim` write lock ensures thread safety for concurrent writes.
- The cache is updated every time a new configuration is received from the server (via SSE or polling).
- On startup (Phase 1), the cache is read synchronously for the ETag-based conditional GET.
- Corrupted or unreadable cache files are treated as a cache miss (graceful degradation).

---

## Real-Time Updates

### SSE Connection Management

1. **Connect**: Open an SSE connection to `/client/config/stream` with the `ApiKey` authorization header and `Accept: text/event-stream`.
2. **Initial config**: The server sends the current config as the first `config` event.
3. **Updates**: When a new snapshot is activated, the server pushes a new `config` event with the full resolved config.
4. **Heartbeat monitoring**: If no event (config or heartbeat) is received within the heartbeat timeout, the connection is considered dead. Uses `System.Net.ServerSentEvents.SseParser` for W3C-compliant SSE parsing.
5. **Reconnect**: On disconnect, use exponential backoff with jitter: base delay doubles up to the configured max delay. First connection attempt has no delay.
6. **Last-Event-ID**: On reconnect, send the last received event ID as `Last-Event-ID` header to resume from the last known position.

### Configuration Reload

When new configuration is received (via SSE or polling):

1. Parse the response and extract key-value pairs (JSON is flattened using `:` separators for nested objects and numeric indices for arrays).
2. Update the `GroundControlStore` with the new snapshot (atomic swap via `volatile` field).
3. The store raises `OnDataChanged`, which triggers the configuration provider's `OnReload()`.
4. `OnReload()` fires .NET's `IOptionsMonitor<T>` and `IOptionsSnapshot<T>` change notifications.
5. Update the local file cache asynchronously.

This means consuming applications can use the standard .NET patterns for reacting to configuration changes:

```csharp
// Automatically gets updated values
services.Configure<DatabaseOptions>(configuration.GetSection("Database"));

// React to changes
services.AddSingleton<IOptionsChangeTokenSource<DatabaseOptions>>(
    new ConfigurationChangeTokenSource<DatabaseOptions>(configuration));
```

---

## Polling Behavior

When in polling mode (or as a fallback from SSE):

1. Send `GET /client/config` with `If-None-Match: "<currentETag>"`.
2. If the server returns `304 Not Modified`, no action needed.
3. If the server returns `200` with new config, process the update.
4. Wait for the configured polling interval with jitter (75-125% of base interval, minimum 100ms).
5. Repeat.

Polling interval is configurable (default: 5 minutes). Jitter is added to prevent all clients from polling simultaneously.

---

## Type Handling

The SDK stores all values as strings in the `IConfiguration` data dictionary (consistent with how .NET configuration works). Nested JSON objects are flattened to colon-separated keys, arrays use numeric indices. Null values are omitted. Type conversion is handled by the consuming application through the standard binding mechanisms:

```csharp
// .NET handles type conversion from string automatically
var timeout = configuration.GetValue<int>("RequestTimeout");
var enabled = configuration.GetValue<bool>("FeatureFlags:NewCheckout");
var deadline = configuration.GetValue<DateOnly>("Project:Deadline");
```

### JSON Flattening Examples

| JSON Input | Flattened Key | Value |
|------------|--------------|-------|
| `{"Database": {"Host": "localhost"}}` | `Database:Host` | `localhost` |
| `{"Hosts": ["a", "b"]}` | `Hosts:0`, `Hosts:1` | `a`, `b` |
| `{"Servers": [{"Host": "s1"}]}` | `Servers:0:Host` | `s1` |
| `{"Key": null}` | *(omitted)* | |
| `{"Count": 42}` | `Count` | `42` |
| `{"Enabled": true}` | `Enabled` | `True` |

---

## Error Handling and Resilience

### Network Errors

| Scenario | Behavior |
|----------|----------|
| Server unreachable on startup | Fall through to cache or empty config. Background service retries. |
| SSE connection drops | Switch to polling mode (if SseWithPollingFallback). Attempt SSE reconnect with exponential backoff. |
| Polling request fails | Retry at next polling interval. Mark health Degraded. |
| Authentication failure (401/403) | Log error. Stop retrying permanently (bad credentials won't become valid). |
| Server error (5xx) | Classified as TransientError. Retry with backoff. |
| Config not found (404) | Classified as NotFound. Mark health Degraded. |

### Data Integrity

- Configuration keys are stored in case-insensitive dictionaries (`StringComparer.OrdinalIgnoreCase`).
- The `GroundControlStore` uses a `volatile` field for thread-safe snapshot swaps.
- Malformed responses are logged and ignored. The current configuration remains unchanged.

### Health Status

The SDK tracks health via `HealthStatus` (from `Microsoft.Extensions.Diagnostics.HealthChecks`):

| Status | Condition |
|--------|-----------|
| Healthy | Successfully loaded/updated from server |
| Degraded | Server unreachable but serving from cache |
| Unhealthy | No configuration data available (no server, no cache) |

Health is reported via `LinkHealthCheck` (registered as `"GroundControl"` health check with configurable tags). The health check result includes metadata: `lastUpdate`, `connectionMode`, `etag` (when healthy), and `LastErrorReason` / exception (when degraded/unhealthy).

### Logging

The SDK uses source-generated `LoggerMessage` methods via `ILogger` for structured, high-performance diagnostics:

| Level | Events |
|-------|--------|
| Information | Data protection unavailable, configuration loaded from cache |
| Warning | Cache read failed, cannot decrypt without data protection |
| Debug | SSE events, heartbeats, 304 Not Modified, fetch results |

---

## Metrics

The SDK provides OpenTelemetry-compatible instrumentation via `System.Diagnostics.Metrics`. The meter name is `GroundControl.Link`.

| Instrument | Type | Description |
|-----------|------|-------------|
| `groundcontrol.link.fetch.count` | Counter | REST fetch attempts (tagged by `status`) |
| `groundcontrol.link.fetch.duration` | Histogram (seconds) | REST fetch request duration |
| `groundcontrol.link.reload.count` | Counter | Configuration reloads (tagged by `source`) |
| `groundcontrol.link.sse.reconnect.count` | Counter | SSE reconnection attempts |
| `groundcontrol.link.sse.connected` | UpDownCounter | 1 when SSE connected, 0 when disconnected |

---

## Authentication

All requests to the server include:

- **`ApiKey` header**: `{ClientId}:{ClientSecret}` with `AuthenticationHeaderValue` scheme `ApiKey`
- **`api-version` header**: API version (default `1.0`)

SSE reconnection requests additionally include:
- **`Last-Event-ID` header**: The last received SSE event ID for stream resumption

---

## Configuration Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ServerUrl` | Uri | *required* | GroundControl server URL |
| `ClientId` | string | *required* | Client ID for authentication |
| `ClientSecret` | string | *required* | Client secret for authentication |
| `ConnectionMode` | enum | `SseWithPollingFallback` | `Sse`, `Polling`, `SseWithPollingFallback`, or `StartupOnly` |
| `StartupTimeout` | TimeSpan | 10 seconds | Max time to wait for server on startup |
| `PollingInterval` | TimeSpan | 5 minutes | Interval for polling mode |
| `SseReconnectDelay` | TimeSpan | 5 seconds | Base delay for SSE reconnection (exponential backoff) |
| `SseMaxReconnectDelay` | TimeSpan | 5 minutes | Maximum delay between SSE reconnection attempts |
| `SseHeartbeatTimeout` | TimeSpan | 2 minutes | Time without events before connection is considered dead |
| `EnableLocalCache` | bool | true | Whether to cache config to disk |
| `CacheFilePath` | string | `./groundcontrol-cache.json` | Path for the local cache file |
| `ApiVersion` | string | `1.0` | API version sent in `api-version` header on all requests |
| `HealthCheckTags` | IList\<string\> | `["ready"]` | Tags applied to the registered health check |

Options are validated eagerly during `AddGroundControl()` using a source-generated `IValidateOptions<GroundControlOptions>` validator combined with `IValidatableObject`. Validation ensures all `TimeSpan` properties are positive and `SseMaxReconnectDelay >= SseReconnectDelay`. Invalid options throw `OptionsValidationException`.
