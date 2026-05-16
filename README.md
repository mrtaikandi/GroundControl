# GroundControl

Centralized, scope-aware configuration management for distributed applications.

## Overview

GroundControl is a self-hosted configuration management server that delivers the right configuration to each instance of your application based on where and how it runs. Instead of maintaining separate config files per environment, you define **scopes** (dimensions such as `Environment`, `Region`, or `AppTier`) and assign values along those dimensions. Each client receives only the configuration that matches its context.

Configuration is distributed as **snapshots**: immutable, versioned bundles built from your project entries and shared templates, with all variables resolved and secrets encrypted. Clients fetch snapshots over REST and stay synchronized in real time over Server-Sent Events (SSE).

The server exposes a language-agnostic REST API, so any application that can speak HTTP can integrate with GroundControl regardless of platform or runtime. Client SDKs are layered on top of the API to make integration even simpler. The first available SDK is `GroundControl.Link` for .NET; SDKs for additional platforms are on the roadmap. A terminal CLI (`groundcontrol`) covers the full management surface.

## Features

- **Scope-aware delivery**: define dimensions (Environment, Region, AppTier, and so on) and deliver the most specific configuration match to each client.
- **Groups and projects**: organize applications into groups and projects with isolated configuration namespaces.
- **Templates**: share common configuration defaults across projects, while each project overrides only what differs.
- **Variables**: reusable named placeholders (`{{variableName}}`) interpolated at publish time.
- **Configuration entries**: type-aware key/value pairs with per-scope values.
- **Immutable snapshots**: point-in-time configuration bundles that are versioned, auditable, and rollback-friendly.
- **Real-time streaming**: clients stay synchronized via SSE with automatic polling fallback.
- **Sensitive values**: encrypted at rest and masked in API responses.
- **Audit trails**: immutable records of every change across the system.
- **Fine-grained RBAC**: roles and grants at the group, project, and scope-filter level.
- **Personal Access Tokens**: scoped, optionally time-bounded tokens for CI/CD pipelines.
- **CLI**: full management surface from the terminal, including an interactive TUI dashboard.
- **Observability**: health checks, OpenTelemetry metrics, tracing, and structured logging.

## How clients connect

Any application can talk to GroundControl through the REST API, which exposes endpoints for authentication, fetching the active snapshot, and subscribing to change notifications over SSE. Two integration paths are supported:

- **Use a client SDK**, when one is available for your platform. The SDK handles authentication, snapshot fetching, SSE subscription, caching, and value resolution so the application can read configuration through native idioms.
- **Call the REST API directly**, from any language that can make HTTP requests and read an SSE stream. This path is the right choice when no SDK exists for your platform yet, or when you need full control over the transport.

### Available SDKs

| SDK | Platform | Status |
|-----|----------|--------|
| [`GroundControl.Link`](src/GroundControl.Link) | .NET | Available |
| Additional platforms | TBD | Planned |

If you would like to see an SDK for a specific platform, open an issue or contribute one. The protocol is documented in the [API guide](docs/guide/api/overview.md).

## Architecture Overview

The GroundControl **server** is built on **.NET 10** with **ASP.NET Core Minimal APIs** and **MongoDB 8** (replica set required for change-stream notifications). Server-side technology choices are an implementation detail of the host; clients only interact with the REST and SSE surface.

| Component | Technology |
|-----------|------------|
| Server runtime | .NET 10, ASP.NET Core Minimal APIs |
| Database | MongoDB 8 (replica set) |
| Real-time delivery | Server-Sent Events via `InProcessChangeNotifier` or `MongoChangeStream` |
| API versioning | Header-based (`api-version`), default v1.0 |
| Observability | OpenTelemetry (metrics, tracing, logging) with OTLP exporter |
| CLI | System.CommandLine 2.0, Spectre.Console, Terminal.Gui |
| Local orchestration | .NET Aspire (`GroundControl.AppHost`) |
| Generated API client | NSwag (`GroundControl.Api.Client`) used by the CLI |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dot.net) (required to run the server and the CLI)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/dotnet-aspire-cli)
- Docker (used by Aspire for MongoDB)

> Note: your client application does not need the .NET SDK. Only the GroundControl server and the management CLI run on .NET. Your applications can be written in any language that can call an HTTP API.

### 1. Start the server

```bash
aspire start src/GroundControl.AppHost
```

The Aspire dashboard opens automatically for monitoring, and the API is available at the URL shown in the dashboard output. The AppHost configures `Authentication__Mode` to `None` (all endpoints open), which is suitable for local development only. Switch to `BuiltIn` or `External` before exposing the server on a network. See [Server Authentication](docs/guide/server/authentication.md) for details.

### 2. Install and use the CLI

```bash
dotnet tool install -g GroundControl.Cli
groundcontrol auth login --server http://localhost:8080
```

Create your first scope, project, and snapshot:

```bash
# Define a scope dimension
groundcontrol scope create --name Environment --values dev,staging,prod

# Create a group and project
groundcontrol group create --name my-team
groundcontrol project create --group my-team --name my-service

# Add a config entry and publish a snapshot
groundcontrol config-entry create --project my-service --key DatabaseUrl --value "postgres://..."
groundcontrol snapshot publish --project my-service
```

Running `groundcontrol` with no arguments opens the interactive TUI dashboard.

### 3. Connect your application

Pick the path that matches your platform.

#### Option A: integrate with a client SDK

If a client SDK is available for your platform, this is the easiest path. For .NET applications, install `GroundControl.Link`:

```bash
dotnet add package GroundControl.Link
```

Register it in `Program.cs`. The SDK uses a two-phase registration: Phase 1 adds GroundControl as an `IConfiguration` source so values are available at startup, and Phase 2 wires up the background services that keep the configuration synchronized at runtime.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Phase 1: add GroundControl as a configuration provider.
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl    = new Uri("http://localhost:8080");
    options.ClientId     = "<client-id>";
    options.ClientSecret = "<client-secret>";
});

// Phase 2: register background services (SSE/polling, health checks, metrics).
builder.Services.AddGroundControl(builder.Configuration);

var app = builder.Build();
```

Configuration values are then available through the standard `IConfiguration` API and refresh in real time. The SDK stays synchronized with the active snapshot via SSE and falls back to polling automatically on reconnect. Bind sections to strongly-typed options classes and inject `IOptionsMonitor<T>` to react to live updates. If you only need configuration at startup, set `ConnectionMode = ConnectionMode.StartupOnly` and skip Phase 2. See the [SDK Quick Start](docs/guide/sdk/quickstart.md) for the full reference.

#### Option B: call the REST API directly

If no SDK exists for your platform yet, your application can integrate by:

1. Authenticating with a client ID and secret to obtain an access token.
2. Fetching the active snapshot for the client from the REST API.
3. Subscribing to change notifications over Server-Sent Events and refetching when a new snapshot is published.

The full endpoint surface is documented in the [API endpoints reference](docs/guide/api/endpoints.md), and conventions like versioning, error responses, and authentication are covered in the [API overview](docs/guide/api/overview.md).

For a guided walkthrough, see the [Getting Started guide](docs/guide/getting-started.md).

## Documentation

### Guides

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/guide/getting-started.md) | End-to-end walkthrough from server setup to scopes, projects, snapshots, and SDK integration |
| [Core Concepts](docs/guide/concepts.md) | Scopes, groups, projects, templates, variables, snapshots, clients |
| [Server Authentication](docs/guide/server/authentication.md) | Authentication modes (None, BuiltIn, External/OIDC) |
| [Server Configuration](docs/guide/server/configuration.md) | All server configuration options |
| [Server Deployment](docs/guide/server/deployment.md) | Multi-instance, high availability, data protection key ring |
| [SDK Quick Start](docs/guide/sdk/quickstart.md) | Connecting with `GroundControl.Link` |
| [SDK Connection Modes](docs/guide/sdk/connection-modes.md) | SSE streaming, polling, and combined mode |
| [SDK Caching](docs/guide/sdk/caching.md) | File cache, null cache, and cache configuration |
| [SDK Options Reference](docs/guide/sdk/options-reference.md) | Full `GroundControlOptions` reference |
| [API Overview](docs/guide/api/overview.md) | REST API overview and conventions |
| [API Endpoints](docs/guide/api/endpoints.md) | Full endpoint reference |

### CLI Reference

| Document | Description |
|----------|-------------|
| [CLI Overview](docs/cli/README.md) | Command index, global options, output formats |
| [Authentication](docs/cli/authentication.md) | `auth`, `user`, `role`, `token` commands |
| [Configuration](docs/cli/configuration.md) | `config`, `config-entry`, `variable` commands |
| [Projects](docs/cli/projects.md) | `group`, `project`, `template`, `scope` commands |
| [Operations](docs/cli/operations.md) | `client`, `client-config`, `snapshot`, `audit` commands |
| [TUI Dashboard](docs/cli/tui.md) | Interactive terminal dashboard (`tui` command) |

## Development

### Prerequisites

- [.NET 10 SDK](https://dot.net) (version pinned in `global.json`)
- Docker (integration tests use Testcontainers for MongoDB, and Aspire uses it for local dev)

### Build

```bash
dotnet build
```

### Test

```bash
# All tests
dotnet test

# Single project
dotnet test tests/GroundControl.Api.Tests
dotnet test tests/GroundControl.Persistence.MongoDb.Tests

# Single test by name filter
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

## License

GroundControl is dual-licensed. See [LICENSING.md](LICENSING.md) for a plain-English summary, [COMMERCIAL.md](COMMERCIAL.md) for commercial licensing, and [CONTRIBUTING.md](CONTRIBUTING.md) for the contributor agreement.
