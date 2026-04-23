# GroundControl

Centralized, scope-aware configuration management for distributed .NET applications.

## Overview

GroundControl is a self-hosted configuration management server that delivers the right configuration to each instance of your application based on where and how it runs. Instead of maintaining separate config files per environment, you define **scopes** — dimensions such as `Environment`, `Region`, or `AppTier` — and assign values along those dimensions. Each client receives only the configuration that matches its context.

Configuration is distributed as **snapshots**: immutable, and versioned bundles built from your project entries and shared templates, with all variables resolved and secrets encrypted. Clients receive snapshots over REST or Server-Sent Events (SSE) and stay synchronized automatically as new snapshots are published.

GroundControl includes a REST API, a terminal CLI (`groundcontrol`), and a .NET SDK (`GroundControl.Link`) for direct integration with `Microsoft.Extensions.Configuration`.

## Features

- **Scope-aware delivery** — Define dimensions (Environment, Region, AppTier, …) and deliver the most specific configuration match to each client
- **Groups & Projects** — Organize applications into groups and projects with isolated configuration namespaces
- **Templates** — Share common configuration defaults across projects; projects override only what differs
- **Variables** — Reusable named placeholders (`{{variableName}}`) interpolated at publish time
- **Configuration entries** — Type-aware key-value pairs with per-scope values
- **Immutable snapshots** — Point-in-time config bundles; versioned, auditable, and rollback-friendly
- **SSE streaming** — Clients stay synchronized in real time; polling fallback is automatic
- **Sensitive values** — Encrypted at rest and masked in API responses
- **Audit trails** — Immutable records of every change across the system
- **Fine-grained RBAC** — Roles and grants at the group, project, and scope-filter level
- **Personal Access Tokens** — Scoped, optionally time-bounded tokens for CI/CD pipelines
- **CLI** — Full management surface from the terminal, including an interactive TUI dashboard
- **Observability** — Health checks, OpenTelemetry metrics, tracing, and structured logging

## Architecture Overview

GroundControl is built on **.NET 10** with **ASP.NET Core Minimal APIs** and **MongoDB 8** (replica set required for change-stream notifications).

| Component | Technology |
|-----------|------------|
| Server runtime | .NET 10, ASP.NET Core Minimal APIs |
| Database | MongoDB 8 (replica set) |
| Real-time delivery | Server-Sent Events via `InProcessChangeNotifier` or `MongoChangeStream` |
| API versioning | Header-based (`api-version`), default v1.0 |
| Observability | OpenTelemetry (metrics, tracing, logging) + OTLP exporter |
| CLI | System.CommandLine 2.0, Spectre.Console, Terminal.Gui |
| Client SDK | `GroundControl.Link` — plugs into `Microsoft.Extensions.Configuration` |
| Local orchestration | .NET Aspire (`GroundControl.AppHost`) |
| Generated API client | NSwag (`GroundControl.Api.Client`) used by the CLI |

## Project Structure

| Project | Description |
|---------|-------------|
| `GroundControl.Api` | Server — vertical feature slices, module system, composition root |
| `GroundControl.Persistence.Abstractions` | Store interfaces and entity types; no external dependencies |
| `GroundControl.Persistence.MongoDb` | MongoDB implementations of all store interfaces |
| `GroundControl.Link` | .NET client SDK — `AddGroundControl()` for `IConfigurationBuilder` |
| `GroundControl.Api.Client` | Generated HTTP client used by the CLI |
| `GroundControl.Cli` | `groundcontrol` CLI executable, packaged as a .NET global tool |
| `GroundControl.Host.Cli` | Reusable CLI framework (System.CommandLine + Spectre.Console) |
| `GroundControl.Host.Api.Generators` | Roslyn source generator for API module infrastructure |
| `GroundControl.AppHost` | .NET Aspire orchestration — local dev with MongoDB replica set |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dot.net)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/dotnet-aspire-cli)
- Docker (used by Aspire for MongoDB)

### 1. Start the server

```bash
aspire start src/GroundControl.AppHost
```

The Aspire dashboard opens automatically for monitoring. The API is available at the URL shown in the dashboard output. The AppHost configures `Authentication__Mode` to `None` (all endpoints open) — suitable for local development only. Switch to `BuiltIn` or `External` before exposing the server on a network. See [Server — Authentication](docs/guide/server/authentication.md) for details.

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

### 3. Connect your .NET application

Install the client SDK:

```bash
dotnet add package GroundControl.Link
```

Register it in `Program.cs`:

```csharp
builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl    = "http://localhost:8080";
    options.ClientId     = "<client-id>";
    options.ClientSecret = "<client-secret>";

    // SSE streaming with automatic polling fallback (default)
    options.ConnectionMode = ConnectionMode.SseWithPollingFallback;
});
```

Configuration values are then available through the standard `IConfiguration` API. The SDK stays synchronized with the active snapshot via SSE and falls back to polling automatically on reconnect.

For a full walkthrough, see the [Getting Started guide](docs/guide/getting-started.md).

## Documentation

### Guides

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/guide/getting-started.md) | End-to-end walkthrough: server → scopes → project → snapshot → SDK |
| [Core Concepts](docs/guide/concepts.md) | Scopes, groups, projects, templates, variables, snapshots, clients |
| [Server — Authentication](docs/guide/server/authentication.md) | Authentication modes (None, BuiltIn, External/OIDC) |
| [Server — Configuration](docs/guide/server/configuration.md) | All server configuration options |
| [Server — Deployment](docs/guide/server/deployment.md) | Multi-instance, high availability, data protection key ring |
| [SDK — Quick Start](docs/guide/sdk/quickstart.md) | Connecting with `GroundControl.Link` |
| [SDK — Connection Modes](docs/guide/sdk/connection-modes.md) | SSE streaming vs polling vs combined mode |
| [SDK — Caching](docs/guide/sdk/caching.md) | File cache, null cache, and cache configuration |
| [SDK — Options Reference](docs/guide/sdk/options-reference.md) | Full `GroundControlOptions` reference |
| [API — Overview](docs/guide/api/overview.md) | REST API overview and conventions |
| [API — Endpoints](docs/guide/api/endpoints.md) | Full endpoint reference |

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
- Docker (integration tests use Testcontainers for MongoDB; Aspire uses it for local dev)

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
