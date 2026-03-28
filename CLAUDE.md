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

Each feature folder under `Api/Features/{FeatureName}/` has:
- **Handler classes** — sealed internal, implementing `IEndpointHandler` with `static abstract void Endpoint(IEndpointRouteBuilder)` for route mapping and private `HandleAsync(...)` for logic
- **`XxxEndpoints.cs`** with `MapXxxEndpoints()` (routes via `MapGroup("/api/xxx").WithTags("Xxx")`) and `AddXxxHandlers()` (DI) extension methods
- **`Contracts/`** subfolder with request/response DTOs
- Handlers registered as `Transient`, resolved via `[FromServices]`

**DI lifetime conventions:** Handlers and validators as `Transient`. Stores, `IMongoDbContext`, `IChangeNotifier`, and `IValueProtector` as `Singleton`. `IDocumentConfiguration` implementations as singletons via `TryAddEnumerable`.

**Request DTOs:** Sealed internal records with `init` properties and Data Annotations (`[Required]`, `[MaxLength]`, etc.).

**Response DTOs:** Sealed internal records with `required init` properties and a static `From(Entity)` factory method for mapping.

**Adding a new feature:** Register handlers in `AddXxxHandlers()`, map routes in `MapXxxEndpoints()`, then wire both into `Program.cs`.

### Data Access — Store Pattern

One specific store interface per entity (no generic base). Methods capture business semantics (e.g., `GetByDimensionAsync`, `HasDependentsAsync`). Optimistic concurrency via `expectedVersion` parameter — updates/deletes filter on both ID and expected version, increment version on success. List operations use `ListQuery` with cursor-based pagination via `MongoCursorPagination` helpers.

MongoDB used directly via `IMongoCollection<T>` with LINQ/Builders — no ORM layer. Per-collection index setup via `DocumentConfiguration<T>` (implements `IDocumentConfiguration`), registered as singleton via `TryAddEnumerable` and run on startup by `MongoIndexSetupService`. Case-insensitive collation from `IMongoDbContext.DefaultCollation` applied to string sorts and unique indexes.

**ETag/Concurrency flow:** GET returns `ETag` header (version). PUT/DELETE require `If-Match` header, parsed via `EntityTagHeaders.TryParseIfMatch()`. Version mismatch → 409 Conflict. Missing header → 428 Precondition Required.

### Validation & Error Handling

- **Input validation:** .NET 10 built-in minimal API validation with Data Annotations on request DTOs
- **Async business validation:** `IAsyncValidator<TRequest>` implementations registered in DI, applied via `.WithContractValidation<T>()` endpoint filter. `IEndpointValidator` + `.WithEndpointValidation<T>()` for non-body validation (route values, headers). Validators return `ValidatorResult.Success`, `ValidatorResult.Fail(error, memberNames)` (→ 400), or `ValidatorResult.Problem(detail, statusCode)` (→ ProblemDetails)
- **Business failures:** `TypedResults.Problem()` returning RFC 9457 ProblemDetails — no custom error types or Result pattern
- **HTTP status conventions:** 400 (validation), 404 (not found), 409 (version conflict / duplicate / has dependents), 422 (semantic business errors, e.g. unresolved variables), 428 (missing If-Match header)

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

## Testing

- **Framework:** xUnit v3 with Shouldly (assertions) and NSubstitute (mocking)
- **Integration tests:** Testcontainers for real MongoDB replica set; `GroundControlApiFactory` (WebApplicationFactory) for API tests
- **Test isolation:** `[Collection("MongoDB")]` shares a single `MongoFixture` container; each test gets its own database via `MongoFixture.CreateDatabase()`
- **Base class:** API handler tests extend `ApiHandlerTestBase`, which provides `CreateFactory()`, `ReadRequiredJsonAsync<T>()`, and `TestCancellationToken`. Each test creates its own `GroundControlApiFactory` instance via `CreateFactory()` for full isolation
- **Structure:** Unit and integration tests coexist in the same test project. Test folders mirror src feature names without the `Features/` prefix (e.g., `tests/.../Scopes/` not `tests/.../Features/Scopes/`)
- **AAA comments:** Use `// Arrange`, `// Act`, `// Assert` in all tests

## Build Infrastructure

- **Versioning:** Nerdbank.GitVersioning (`version.json`), version 1.0
- **Package management:** Central Package Management (`Directory.Packages.props`)
- **Artifacts output:** `UseArtifactsOutput` enabled, output to `artifacts/` at repo root
- **Warnings as errors:** Enabled globally
- **Auto-formatting hook:** `.claude/hooks/format-csharp.cs` runs `dotnet format` on every .cs file after Write/Edit

## Commit Convention

Conventional Commits format: `<type>[scope]: <description>`. See `.github/git-commit-instructions.md` for full details. Scope derived from last segment of project name, lowercased (e.g., `GroundControl.Api` -> `api`).
Do **not** append `Co-Authored-By` trailers or any AI attribution to commits or PR descriptions.

## Planning

Detailed implementation plan and task breakdowns are in `planning/`. The `planning/Implementation-Plan.md` is the authoritative blueprint for architecture, patterns, and build order.