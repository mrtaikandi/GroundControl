# M17 — E2E Integration Tests

**Status:** in-progress
**Dependencies:** M04 (Client Delivery Plane), M07 (Client SDK), M10-M15 (CLI Suite)

## Goal

Build an E2E test suite that validates full workflow chains across CLI, API, and Link SDK
against a real Docker Compose stack. Proves the entire vertical chain works: CLI creates
data, API persists it, Link SDK receives it.

## Tasks

- T082: Scaffold E2E test project with Docker Compose file
- T083: DockerComposeFixture for compose lifecycle management
- T084: CliRunner and CliResult for process-level CLI invocation
- T085: StepAttribute and StepOrderer for ordered scenario workflows
- T086: E2ETestBase with CLI, API client, and Link SDK helpers
- T087: ConfigDeliveryWorkflow scenario (create -> config -> publish -> receive)

## Success Criteria

- `dotnet test tests/GroundControl.E2E.Tests` starts a Docker stack, runs the
  ConfigDeliveryWorkflow (5 steps), and tears down the stack
- CLI is invoked as a real child process with `--output json`
- API verification uses the generated GroundControlClient
- Link SDK receives the correct configuration values
- Step ordering works: steps execute in order 1-5
- Skip-on-failure works: if step N fails, steps N+1..5 are skipped
- Dual-mode: tests also run when E2E_RUNNING_IN_DOCKER=true with E2E_API_URL set

## Design Spec

`docs/superpowers/specs/2026-04-05-e2e-integration-tests-design.md`