---
paths:
  - "src/GroundControl.Api/Features/**/*.cs"
  - "src/GroundControl.Api/Core/**/*.cs"
---

# Feature Slice Pattern

Each feature folder under `Api/Features/{FeatureName}/` has:
- **`XxxModule.cs`** — implements `IWebApiModule`, registers handlers/validators in DI and maps routes via `MapGroup("/api/xxx").WithTags("Xxx")`
- **Handler classes** — sealed internal, one per endpoint, implementing `IEndpointHandler` with `static abstract void Endpoint(IEndpointRouteBuilder)` for route mapping and private `HandleAsync(...)` for logic
- **`Contracts/`** subfolder with request/response DTOs
- Handlers registered as `Transient`, resolved via `[FromServices]`

**Adding a new feature:** Create a `XxxModule : IWebApiModule`, register handlers and validators in `OnServiceConfiguration`, map routes in `OnApplicationConfiguration`.

## DI Lifetime Conventions

Handlers and validators as `Transient`. Stores, `IMongoDbContext`, `IChangeNotifier`, and `IValueProtector` as `Singleton`. `IDocumentConfiguration` implementations as singletons via `TryAddEnumerable`.

## DTOs

**Request DTOs:** Sealed internal records with `init` properties and Data Annotations (`[Required]`, `[MaxLength]`, etc.).

**Response DTOs:** Sealed internal records with `required init` properties and a static `From(Entity)` factory method for mapping.

## Validation & Error Handling

- **Input validation:** .NET 10 built-in minimal API validation with Data Annotations on request DTOs
- **Async business validation:** `IAsyncValidator<TRequest>` in DI, applied via `.WithContractValidation<T>()` endpoint filter. `IEndpointValidator` + `.WithEndpointValidation<T>()` for non-body validation (route values, headers). Validators return `ValidatorResult.Success`, `.Fail(error, memberNames)` (-> 400), or `.Problem(detail, statusCode)` (-> ProblemDetails)
- **Business failures:** `TypedResults.Problem()` returning RFC 9457 ProblemDetails — no custom error types or Result pattern
- **HTTP status conventions:** 400 (validation), 404 (not found), 409 (version conflict / duplicate / has dependents), 422 (semantic business errors), 428 (missing If-Match header)

## Api Folder Placement Rules

- **Core/** — extends ASP.NET, provides pipeline infrastructure, or defines contracts that handlers implement (*how* the API runs)
- **Shared/** — reusable business logic consumed across features (*what* the API does)
- **Extensions/** — extension methods on types the project does not own
- **Features/** — one folder per business domain, fully self-contained. Features never depend on each other; they share data through store interfaces in `Persistence.Abstractions`