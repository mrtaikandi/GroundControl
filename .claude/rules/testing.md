---
paths:
  - "tests/**/*.cs"
---

# Testing Conventions

- **Framework:** xUnit v3 with Shouldly (assertions) and NSubstitute (mocking)
- **Integration tests:** Testcontainers for real MongoDB replica set; `GroundControlApiFactory` (WebApplicationFactory) for API tests
- **Base class:** API handler tests extend `ApiHandlerTestBase`, which provides `CreateFactory()`, `ReadRequiredJsonAsync<T>()`, and `TestCancellationToken`. Each test creates its own `GroundControlApiFactory` instance via `CreateFactory()` for full isolation
- **Structure:** Unit and integration tests coexist in the same test project. Test folders mirror src feature names without the `Features/` prefix (e.g., `tests/.../Scopes/` not `tests/.../Features/Scopes/`)
- **Shared infra:** `GroundControl.Api.Tests.Infrastructure` provides shared test utilities for API test projects
- **E2E tests:** `GroundControl.E2E.Tests` uses Aspire.Hosting.Testing to spin up the full distributed app. Tests use ordered steps (`StepAttribute`/`StepOrderer`) with shared state and failure tracking. Runs CLI commands against a live Aspire app via `CliRunner`
- **AAA comments:** Use `// Arrange`, `// Act`, `// Assert` in all tests