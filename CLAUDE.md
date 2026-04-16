# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GroundControl is a centralized configuration management server with a .NET client SDK. It manages scoped configuration entries, templates, variables, and snapshots that are delivered to clients via REST and SSE. Built on .NET 10, ASP.NET Minimal APIs, and MongoDB.

## Build & Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/GroundControl.Api.Tests
dotnet test tests/GroundControl.Persistence.MongoDb.Tests

# Run a specific test by filter
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"

# Format code (enforced by post-tool-use hook on .cs files)
dotnet format
dotnet format --include path/to/File.cs --no-restore

# Check formatting without applying
dotnet format --verify-no-changes
```

## Architecture

**Vertical Slice Architecture** — each feature is a self-contained handler class, not layered by technical concern.

**Dependency flow:** `Api -> Persistence.Abstractions <- Persistence.MongoDb`. Feature code depends only on abstractions. MongoDb is referenced by Api only for DI registration. `Cli -> Api.Client -> Api` (for NSwag generation). `Host.Cli` is a standalone framework referenced by `Cli`.

### Module System

Each feature and core concern is an `IWebApiModule` with `OnServiceConfiguration` and `OnApplicationConfiguration`. Modules declare ordering via `[RunsAfter<T>]` attributes. `Program.cs` delegates to `BuildWebApiModules()`.

### Cross-Cutting Infrastructure

- **API versioning:** Header-based (`api-version` header), default version 1.0
- **Health checks:** `/healthz/liveness` (no checks) and `/healthz/ready` (MongoDB + change notifier)
- **Change notification:** `IChangeNotifier` with `NotifyAsync`/`SubscribeAsync` for fan-out pub/sub (currently `InProcessChangeNotifier` using `Channel<T>` per subscriber)
- **Auth configurators:** Pluggable `IAuthConfigurator` — currently `NoAuthConfigurator` for development; `BuiltIn` and `External` modes planned

### Key Conventions

- **IDs:** `Guid` using `Guid.CreateVersion7()` at creation
- **CancellationToken:** Always `CancellationToken cancellationToken = default` (full name, default value)
- **ConfigureAwait:** All library/non-API code uses `.ConfigureAwait(false)`
- **Records:** Use `init` properties, not positional parameters (primary constructors)
- **Namespaces:** File-scoped
- **Usings:** Project-specific global usings in `Properties/Usings.cs`
- **Entities:** Classes (not records) with get/set properties, include `Version` (long), `CreatedAt`/`UpdatedAt` (DateTimeOffset), `CreatedBy`/`UpdatedBy` (Guid)

## Build Infrastructure

- **Package management:** Central Package Management (`Directory.Packages.props`)
- **Versioning:** Nerdbank.GitVersioning (`version.json`)
- **Auto-formatting hook:** `.claude/hooks/format-csharp.cs` runs `dotnet format` on every .cs file after Write/Edit

## Commit Convention

Conventional Commits format: `<type>[scope]: <description>`. See `.github/git-commit-instructions.md` for full details. Scope derived from last segment of project name, lowercased (e.g., `GroundControl.Api` -> `api`).
Do **not** append `Co-Authored-By` trailers or any AI attribution to commits or PR descriptions.

## Planning

Detailed implementation plan and task breakdowns are in `planning/`. The `planning/Implementation-Plan.md` is the authoritative blueprint for architecture, patterns, and build order.