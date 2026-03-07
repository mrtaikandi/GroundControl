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

### Project Structure (4 src + 3 test projects)

```
src/
  GroundControl.Persistence.Abstractions/  # Store interfaces + entity types (no external deps)
  GroundControl.Persistence.MongoDb/       # MongoDB store implementations
  GroundControl.Api/                       # Features + shared infra (composition root)
  GroundControl.Link/                      # Client SDK (NuGet package)
tests/
  GroundControl.Api.Tests/
  GroundControl.Persistence.MongoDb.Tests/
  GroundControl.Link.Tests/
```

**Dependency flow:** `Api -> Persistence.Abstractions <- Persistence.MongoDb`. Feature code depends only on abstractions. MongoDb is referenced by Api only for DI registration.

### Feature Slice Pattern

Each feature folder has:
- **Handler classes** implementing `IEndpointHandler` with `static abstract void Endpoint(IEndpointRouteBuilder)` for route mapping and `HandleAsync(...)` for logic
- **`XxxEndpoints.cs`** with `MapXxxEndpoints()` (routes) and `AddXxxHandlers()` (DI) extension methods
- Handlers registered as `Transient`, resolved via `[FromServices]`

### Data Access — Store Pattern

One specific store interface per entity (no generic base). Methods capture business semantics (e.g., `GetByDimensionAsync`, `IsReferencedAsync`). Optimistic concurrency via `expectedVersion` parameter. List operations use `ListQuery` or entity-specific derived query types.

MongoDB used directly via `IMongoCollection<T>` with LINQ/Builders — no ORM layer. Per-collection index setup via `IDocumentConfiguration<T>`.

### Validation & Error Handling

- Input validation: .NET 10 built-in minimal API validation with Data Annotations on request DTOs
- Business failures: `TypedResults.Problem()` returning RFC 9457 ProblemDetails — no custom error types or Result pattern

### Key Conventions

- **IDs:** `Guid` using `Guid.CreateVersion7()` at creation
- **CancellationToken:** Always `CancellationToken cancellationToken = default` (full name, default value)
- **Records:** Use `init` properties, not positional parameters (primary constructors)
- **Namespaces:** File-scoped
- **Usings:** Project-specific global usings in `Properties/Usings.cs`

## Testing

- **Framework:** xUnit v3 with Shouldly (assertions) and NSubstitute (mocking)
- **Integration tests:** Testcontainers for real MongoDB; WebApplicationFactory for API tests
- **Structure:** Unit and integration tests coexist in the same test project
- **AAA comments:** Use `// Arrange`, `// Act`, `// Assert` in all tests

## Build Infrastructure

- **Versioning:** Nerdbank.GitVersioning (`version.json`), version 1.0
- **Package management:** Central Package Management (`Directory.Packages.props`)
- **Artifacts output:** `UseArtifactsOutput` enabled, output to `artifacts/` at repo root
- **Warnings as errors:** Enabled globally
- **Auto-formatting hook:** `.claude/hooks/format-csharp.cs` runs `dotnet format` on every .cs file after Write/Edit

## Commit Convention

Conventional Commits format: `<type>[scope]: <description>`. See `.github/git-commit-instructions.md` for full details. Scope derived from last segment of project name, lowercased (e.g., `GroundControl.Api` -> `api`).

## Planning

Detailed implementation plan and task breakdowns are in `planning/`. The `planning/Implementation-Plan.md` is the authoritative blueprint for architecture, patterns, and build order.