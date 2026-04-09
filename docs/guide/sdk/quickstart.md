# SDK Quickstart

## Prerequisites

- A running GroundControl server with a published snapshot
- A client ID and secret (see [Getting Started](../getting-started.md))
- .NET 10 or later

## Install the package

```bash
dotnet add package GroundControl.Link
```

## Register GroundControl

The SDK uses a two-phase registration model:

1. **Phase 1** -- Add GroundControl as a configuration source on `IConfigurationBuilder`. This fetches the initial configuration at startup and makes it available through `IConfiguration`.
2. **Phase 2** -- Register background services on `IServiceCollection`. This enables real-time updates (SSE/polling), health checks, and metrics.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Phase 1: Add GroundControl as a configuration provider
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = new Uri("https://groundcontrol.example.com");
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
});

// Phase 2: Register background services (SSE/polling, health checks, metrics)
builder.Services.AddGroundControl(builder.Configuration);

var app = builder.Build();
```

> **Tip:** If you only need configuration at startup and don't need live updates, set `ConnectionMode = ConnectionMode.StartupOnly` and skip Phase 2. See [Connection Modes](connection-modes.md).

## Read configuration values

### Direct access

Retrieve individual values by key:

```csharp
var logLevel = builder.Configuration["App:LogLevel"];
```

### Options pattern (recommended for structured config)

Bind a configuration section to a strongly-typed class:

```csharp
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));

public class DatabaseSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Name { get; set; }
}
```

### Live updates with IOptionsMonitor

When a new snapshot is activated on the server, the SDK receives the update in
real time (via SSE) and triggers change notifications. `IOptionsMonitor<T>`
always returns the latest values. `IOptionsSnapshot<T>` gives you a consistent
view per request.

```csharp
app.MapGet("/settings", (IOptionsMonitor<DatabaseSettings> monitor) =>
{
    var current = monitor.CurrentValue;
    return new { current.Host, current.Port };
});
```

## Store credentials securely

> **Note:** Don't hard-code credentials. Use environment variables, user secrets, or a vault:

```csharp
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = new Uri(builder.Configuration["GroundControl:ServerUrl"]!);
    options.ClientId = builder.Configuration["GroundControl:ClientId"]!;
    options.ClientSecret = builder.Configuration["GroundControl:ClientSecret"]!;
});

builder.Services.AddGroundControl(builder.Configuration);
```

## Customize the HTTP client

Phase 2 accepts an optional delegate to configure the `HttpClient` used by the background services:

```csharp
builder.Services.AddGroundControl(
    builder.Configuration,
    configureHttpClient: httpBuilder =>
    {
        httpBuilder.AddStandardResilienceHandler();
    });
```

## What's next?

- [Connection Modes](connection-modes.md) -- choose between SSE, polling, hybrid, or startup-only
- [Caching](caching.md) -- offline resilience and local file cache
- [Options Reference](options-reference.md) -- every configuration property