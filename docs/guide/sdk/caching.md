# Caching and Offline Resilience

The SDK can persist configuration to a local file so your application starts even when the server is unreachable.

## Enabling the cache

The cache is enabled by default. Customize the file path if needed:

```csharp
builder.Configuration.AddGroundControl(options =>
{
    // ...
    options.EnableLocalCache = true; // default
    options.CacheFilePath = "/var/cache/myapp/groundcontrol.json";
});
```

To disable caching:

```csharp
options.EnableLocalCache = false;
```

## How the cache works

- Every time the SDK receives a configuration update (via SSE or polling), it writes the data to the cache file.
- Writes are atomic (the SDK writes to a temporary file first, then renames it) to prevent corruption.
- The cache file is a JSON file containing the configuration key-value pairs.

## Fallback chain on startup

When your application starts, the SDK tries to load configuration in this order:

1. **Server** -- Connect to the GroundControl server (within `StartupTimeout`, default 10 seconds).
2. **Local cache** -- If the server is unreachable, load from the cache file.
3. **Other configuration providers** -- If no cache exists, the application falls back to whatever other providers are registered (for example, `appsettings.json`).

After falling back to the cache, the SDK continues trying to reach the server in the background. Once the server becomes available, configuration is updated automatically.

```mermaid
graph TD
    A[App starts] --> B{Server reachable?}
    B -->|Yes| C[Load from server]
    C --> D[Update cache file]
    D --> E[App running with live config]
    B -->|No| F{Cache file exists?}
    F -->|Yes| G[Load from cache]
    G --> H[App running with cached config]
    H --> I[Retry server in background]
    I -->|Server comes back| C
    F -->|No| J[Use other config providers]
    J --> K[App running with fallback config]
    K --> I
```

## Cache file security

- The cache file may contain sensitive configuration values.
- On Windows, sensitive values are encrypted using DPAPI (tied to the machine or user account).
- On Linux and containers, sensitive values are encrypted using ASP.NET Data Protection.
- The cache is not portable across machines -- a cache file from one machine cannot be read by another.

> **Warning:** Ensure the cache file directory has appropriate file system permissions. The file contains your application's configuration.

## When to disable caching

- Short-lived batch jobs that always have server connectivity.
- Environments where writing to the local file system is not permitted.
- When you do not want any configuration persisted outside the server.

## What's next?

- [Connection Modes](connection-modes.md) -- choose how the SDK connects
- [Options Reference](options-reference.md) -- all cache-related options
