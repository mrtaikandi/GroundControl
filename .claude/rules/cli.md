---
paths:
  - "src/GroundControl.Cli/**/*.cs"
  - "src/GroundControl.Host.Cli/**/*.cs"
  - "src/GroundControl.Api.Client/**/*.cs"
  - "tests/GroundControl.Cli.Tests/**/*.cs"
  - "tests/GroundControl.Api.Client.Tests/**/*.cs"
---

# CLI Architecture

`GroundControl.Cli` is a .NET global tool (`PackAsTool=true`, command: `groundcontrol`). Built on `GroundControl.Host.Cli` which wraps System.CommandLine 2.0 with DI, logging, and Spectre.Console. CLI features mirror API features (Auth, Group, Project, Scope, ConfigEntry, Variable, Template, Snapshot, Audit, etc.). Includes a Terminal.Gui TUI dashboard.

`GroundControl.Api.Client` is NSwag-generated from the API's OpenAPI spec (`nswag.json`). Auto-generated via MSBuild target `GenerateApiClient`.