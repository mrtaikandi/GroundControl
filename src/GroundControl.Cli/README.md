# GroundControl CLI

Command-line tool for managing a [GroundControl](../../README.md) configuration server. Packaged as a .NET global tool.

## Installation

```bash
dotnet tool install -g GroundControl.Cli
```

## Quick Start

```bash
# Login to a server
groundcontrol auth login --server-url https://groundcontrol.example.com --method Pat --token <your-token>

# Create organizational structure
groundcontrol scope create --dimension Environment --values "dev,staging,prod"
groundcontrol group create --name "Backend Services"
groundcontrol project create --name "OrderService" --group-id <group-id>

# Add configuration and publish
groundcontrol config-entry create --key "Database:ConnectionString" \
  --owner-id <project-id> --owner-type Project --value-type String \
  --value "default=Server=localhost"
groundcontrol snapshot publish --project-id <project-id>
```

## Global Options

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `--debug` | `verbose` | -- | Enable debug logging (`--debug` for Information, `--debug verbose` for Debug) |
| `--output` | `table`, `json` | `table` | Output format |
| `--no-interactive` | -- | `false` | Disable interactive prompts and use defaults |

## Commands

| Command | Description |
|---------|-------------|
| `auth` | Login, logout, and authentication status |
| `group` | Create, list, update, and delete groups |
| `project` | Create, list, update, and delete projects |
| `scope` | Create, list, update, and delete scope dimensions |
| `template` | Create, list, update, and delete shared templates |
| `variable` | Create, list, update, and delete reusable variables |
| `config-entry` | Create, list, update, and delete configuration entries |
| `snapshot` | Publish and list configuration snapshots |
| `client` | Create, list, update, and delete API clients |
| `client-config` | Preview resolved configuration for a client |
| `user` | Manage users |
| `role` | Manage roles and permissions |
| `token` | Manage personal access tokens |
| `audit` | Query the audit trail |
| `tui` | Launch the interactive TUI dashboard |

## TUI Dashboard

Running `groundcontrol` with no arguments launches an interactive terminal dashboard built with [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui). The TUI provides a navigable view of groups, projects, configuration entries, and snapshots.

## Configuration

The CLI reads configuration from these sources (in order of precedence):

1. Command-line arguments
2. Environment variables (e.g., `GroundControl__ServerUrl`)
3. `appsettings.local.json` (user-specific overrides)
4. `appsettings.json` (bundled defaults)

## Documentation

See the full [CLI reference](../../docs/cli/README.md) for detailed command documentation.
