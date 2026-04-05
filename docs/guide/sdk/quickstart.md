# SDK Quickstart

## Prerequisites

- A running GroundControl server with a published snapshot
- A client ID and secret (see [Getting Started](../getting-started.md))
- .NET 10 or later

## Install the package

```bash
dotnet add package GroundControl.Link
```

## Add GroundControl to your configuration

Call `AddGroundControl` on your configuration builder to register the SDK as a
standard .NET configuration provider. All values from the active snapshot become
available through `IConfiguration`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = "https://groundcontrol.example.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
});

var app = builder.Build();
```

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
    options.ServerUrl = builder.Configuration["GroundControl:ServerUrl"]!;
    options.ClientId = builder.Configuration["GroundControl:ClientId"]!;
    options.ClientSecret = builder.Configuration["GroundControl:ClientSecret"]!;
});
```

## What's next?

- [Connection Modes](connection-modes.md) -- choose between SSE, polling, or hybrid
- [Caching](caching.md) -- offline resilience and local file cache
- [Options Reference](options-reference.md) -- every configuration property
