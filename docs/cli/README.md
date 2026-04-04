# GroundControl CLI Reference

## Overview

GroundControl CLI is a command-line tool for managing the GroundControl configuration server. It provides commands for authentication, project management, configuration entries, variables, snapshots, and more.

Built on [System.CommandLine](https://github.com/dotnet/command-line-api), the CLI supports both direct command execution and an interactive TUI dashboard.

## Installation

The CLI is currently built from source:

```bash
dotnet build src/GroundControl.Cli
```

The resulting binary is `groundcontrol`.

## Global Options

These options apply to all commands:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--debug` | string? | -- | Enable debug logging. Use `--debug` for Information level or `--debug verbose` for Debug level |
| `--output` | `Table` \| `Json` | `Table` | Output format for command results |
| `--no-interactive` | flag | `false` | Disable interactive prompts and use defaults |

## Default Behavior

Running `groundcontrol` with no arguments launches the interactive TUI dashboard. See [TUI](tui.md) for details.

## Quick Start

The following walkthrough demonstrates a typical end-to-end workflow. Replace placeholder IDs (such as `<group-id>`) with actual values from the output of each create command.

**1. Login to a server**

```bash
groundcontrol auth login --server-url https://gc.example.com --method Pat --token my-token
```

**2. Create a group**

```bash
groundcontrol group create --name "Backend Services"
```

**3. Create scopes**

```bash
groundcontrol scope create --dimension Environment --values "dev,staging,prod"
```

**4. Create a template**

```bash
groundcontrol template create --name "Microservice Defaults" --group-id <group-id>
```

**5. Create a project**

```bash
groundcontrol project create --name "OrderService" --group-id <group-id> --template-ids <template-id>
```

**6. Add a configuration entry**

```bash
groundcontrol config-entry create --key "Database:ConnectionString" --owner-id <project-id> --owner-type Project --value-type String --value "default=Server=localhost"
```

**7. Publish a snapshot**

```bash
groundcontrol snapshot publish --project-id <project-id>
```

> **Note:** Replace placeholder IDs with actual values from create command output.

## Command Reference

| Domain | Commands | Description |
|--------|----------|-------------|
| [Authentication](authentication.md) | `auth`, `user`, `role`, `token` | Server credentials, user management, roles, and access tokens |
| [Configuration](configuration.md) | `config`, `config-entry`, `variable` | CLI config, configuration entries, and variables |
| [Projects](projects.md) | `group`, `project`, `template`, `scope` | Organizational hierarchy and scope dimensions |
| [Operations](operations.md) | `client`, `client-config`, `snapshot`, `audit` | Client management, config resolution, snapshots, and audit trail |
| [TUI](tui.md) | `tui` | Interactive terminal dashboard |
