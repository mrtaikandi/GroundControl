# Client SDK Design

This document defines the design of the GroundControl .NET client SDK, which integrates with the standard `IConfiguration` system to provide real-time configuration from GroundControl.

See [API Design](API-Design.md) for the Client API endpoints and [Security Model](Security-Model.md) for authentication details.

---

## Overview

The client SDK is a NuGet package (e.g., `GroundControl.Link`) that provides an `IConfigurationProvider` implementation. It plugs into .NET's standard configuration pipeline and delivers configuration from GroundControl with automatic real-time updates.

### Key Characteristics

- Integrates with `IConfigurationBuilder` via a standard extension method.
- Provides real-time updates via SSE without application restarts.
- Implements a multi-tier fallback chain for resilience.
- Handles sensitive values transparently.
- Supports .NET's hierarchical key model (colon-separated keys like `Logging:LogLevel:Default`).

---

## Integration

### Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = "https://groundcontrol.example.com";
    options.ClientId = "0192d4e0-7b3a-7f2e-8a1c-4d5e6f7a8b9c";
    options.ClientSecret = "a1b2c3d4e5f6...";

    // Optional settings
    options.CacheFilePath = "./groundcontrol-cache.json";
    options.PollingInterval = TimeSpan.FromMinutes(5);
    options.SseReconnectDelay = TimeSpan.FromSeconds(5);
    options.SseHeartbeatTimeout = TimeSpan.FromMinutes(2);
    options.ConnectionMode = ConnectionMode.Sse; // Sse, Polling, or SseWithPollingFallback (default)
});
```

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

The recommended mode. Attempts SSE first for real-time updates, automatically falls back to polling if SSE fails.

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

Periodically polls the REST endpoint at a configurable interval. Suitable for environments where SSE is not supported (e.g., certain proxies or firewalls).

---

## Fallback Chain

When the application starts, the SDK attempts to load configuration in this order:

```
1. SSE/REST from GroundControl server
   │ Success → Use server config, update local cache
   │ Failure ↓
   │
2. Local file cache (last known good config)
   │ Exists → Use cached config, keep trying server in background
   │ Missing ↓
   │
3. appsettings.json (standard .NET config)
   │ Always available as base configuration
```

### Startup Behavior

| Server available | Cache exists | Behavior |
|-----------------|--------------|----------|
| Yes | Any | Load from server, update cache |
| No | Yes | Load from cache, retry server in background |
| No | No | App starts with appsettings.json defaults. SDK retries server in background. |

The SDK never blocks startup indefinitely. A configurable timeout (default: 10 seconds) limits the initial server connection attempt. If the timeout expires, the SDK falls through to the next tier.

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

The local file cache stores the last successfully received configuration to disk.

### Cache File Format

```json
{
  "snapshotId": "0192d4e0-7b3a-7f2e-8a1c-4d5e6f7a8b9c",
  "snapshotVersion": 12,
  "timestamp": "2024-01-15T10:30:00Z",
  "entries": {
    "Logging:LogLevel:Default": "Warning",
    "Database:ConnectionString": "***ENCRYPTED:<ciphertext>"
  }
}
```

The `entries` field contains flat key-value pairs. When an `IDataProtectionProvider` is supplied directly to the `FileConfigCache` constructor, all values are encrypted with a `***ENCRYPTED:` prefix. Without data protection, values are stored in plaintext. Note: the standard `AddGroundControl(...)` extension method does not currently support supplying a data protection provider, since `IConfigurationSource.Build()` runs before the DI container is finalized.

### Sensitive Value Protection in Cache

Sensitive values in the local cache file are encrypted using the platform's data protection mechanism:

| Platform | Protection Method |
|----------|-----------------|
| Windows | DPAPI (Data Protection API) - encrypted to the machine or user context |
| Linux/Container | ASP.NET Core Data Protection with a configured key ring |

The encryption key is derived from the machine identity, so the cache file is not portable between machines.

### Cache Update Strategy

- The cache is updated atomically (write to temp file, then rename) to prevent corruption.
- The cache is updated every time a new configuration is received from the server (via SSE or polling).
- On startup, the cache is read only if the server is unreachable.

---

## Real-Time Updates

### SSE Connection Management

1. **Connect**: Open an SSE connection to `/client/config/stream` with the API key.
2. **Initial config**: The server sends the current config as the first `config` event.
3. **Updates**: When a new snapshot is activated, the server pushes a new `config` event with the full resolved config.
4. **Heartbeat monitoring**: If no event (config or heartbeat) is received within the heartbeat timeout, the connection is considered dead.
5. **Reconnect**: On disconnect, use exponential backoff with jitter: 1s, 2s, 4s, 8s, ... up to the configured max delay.
6. **Last-Event-ID**: On reconnect, send the last received snapshot ID as `Last-Event-ID` to avoid processing stale data.

### Configuration Reload

When new configuration is received (via SSE or polling):

1. Parse the response and extract key-value pairs.
2. Compare with the current in-memory configuration.
3. If different, update the internal data dictionary.
4. Call `OnReload()` on the configuration provider to trigger .NET's `IOptionsMonitor<T>` and `IOptionsSnapshot<T>` change notifications.
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

1. Send `GET /client/config` with `If-None-Match: "<currentVersion>"`.
2. If the server returns `304 Not Modified`, no action needed.
3. If the server returns `200` with new config, process the update.
4. Wait for the configured polling interval.
5. Repeat.

Polling interval is configurable (default: 5 minutes). Jitter is added to prevent all clients from polling simultaneously.

---

## Type Handling

The SDK stores all values as strings in the `IConfiguration` data dictionary (consistent with how .NET configuration works). Type conversion is handled by the consuming application through the standard binding mechanisms:

```csharp
// .NET handles type conversion from string automatically
var timeout = configuration.GetValue<int>("RequestTimeout");
var enabled = configuration.GetValue<bool>("FeatureFlags:NewCheckout");
var deadline = configuration.GetValue<DateOnly>("Project:Deadline");
```

---

## Error Handling and Resilience

### Network Errors

| Scenario | Behavior |
|----------|----------|
| Server unreachable on startup | Fall through to cache or appsettings.json. Retry in background. |
| SSE connection drops | Switch to polling mode. Attempt SSE reconnect with exponential backoff. |
| Polling request fails | Retry at next polling interval. Log warning. |
| Authentication failure (401) | Log error. Stop retrying (bad API key won't become valid). |
| Server error (5xx) | Retry with exponential backoff. |

### Data Integrity

- The SDK validates that received configuration has a `snapshotVersion` equal to or greater than the currently loaded version. Older versions are ignored (protects against out-of-order delivery during reconnection).
- Malformed responses are logged and ignored. The current configuration remains unchanged.

### Logging

The SDK uses `ILogger` (injected from the host's DI container) for diagnostics:

| Level | Events |
|-------|--------|
| Information | Connected to server, configuration updated (version X → Y), switched to polling mode |
| Warning | Connection lost, polling failed, server returned 5xx, cache file read failed |
| Error | Authentication failed, configuration parse error |
| Debug | Heartbeat received, 304 Not Modified, cache updated |

---

## Configuration Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ServerUrl` | string | *required* | GroundControl server URL |
| `ClientId` | string | *required* | Client ID (from key creation) |
| `ClientSecret` | string | *required* | Client secret (from key creation) |
| `ConnectionMode` | enum | `SseWithPollingFallback` | `Sse`, `Polling`, or `SseWithPollingFallback` |
| `StartupTimeout` | TimeSpan | 10 seconds | Max time to wait for server on startup |
| `PollingInterval` | TimeSpan | 5 minutes | Interval for polling mode |
| `SseReconnectDelay` | TimeSpan | 5 seconds | Base delay for SSE reconnection (exponential backoff) |
| `SseMaxReconnectDelay` | TimeSpan | 5 minutes | Maximum delay between SSE reconnection attempts |
| `SseHeartbeatTimeout` | TimeSpan | 2 minutes | Time without events before connection is considered dead |
| `EnableLocalCache` | bool | true | Whether to cache config to disk |
| `CacheFilePath` | string | `./groundcontrol-cache.json` | Path for the local cache file |
| `ApiVersion` | string | `1.0` | API version sent in `api-version` header on all requests |
| `LoggerFactory` | ILoggerFactory? | null | Logger factory for SDK diagnostics. If not set, logging is disabled. |

---

## Package Structure

```
GroundControl.Link
├── GroundControlConfigurationSource          : IConfigurationSource
├── GroundControlConfigurationProvider        : ConfigurationProvider
├── GroundControlOptions                      : Configuration options
├── ISseClient                                : SSE connection abstraction
│   └── DefaultSseClient                      : HttpClient-based SSE implementation
├── IConfigFetcher                            : REST polling abstraction
│   └── DefaultConfigFetcher                  : HttpClient-based REST implementation
├── IConfigCache                              : Local cache abstraction
│   ├── FileConfigCache                       : File-based cache implementation
│   └── NullConfigCache                       : No-op implementation (when caching is disabled)
└── ConfigurationBuilderExtensions            : AddGroundControl() extension method
```

Abstractions (`ISseClient`, `IConfigFetcher`, `IConfigCache`) allow consuming applications to replace components if needed (e.g., custom cache backed by SQLite instead of a file).

> **Naming note:** `GroundControlOptions` in `GroundControl.Link` is the client SDK configuration type. The Management API host may also use a server-side root `GroundControlOptions` for application startup configuration. They are separate types in different projects.
