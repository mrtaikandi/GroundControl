# Deploying GroundControl

> **Heads up:** GroundControl is still under active development. This guide covers **local development only** — running the server on your machine for evaluation or contribution. Production-deployment guidance (multi-instance hardening, Data Protection key management, change-notifier topology, container images) will be published in a later release.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/dotnet-aspire-cli)
- [Docker](https://docs.docker.com/get-docker/) (used by Aspire to run MongoDB)

## Local development with Aspire (recommended)

The `GroundControl.AppHost` project orchestrates a standalone MongoDB container and the API via the .NET Aspire hosting integrations. A single command starts everything:

```bash
aspire start src/GroundControl.AppHost
```

The Aspire dashboard opens automatically and lists each resource with its URL, logs, and health. The API is pre-configured with `Authentication:Mode = None` and `DataProtection:Mode = FileSystem`, which is suitable for local experimentation only.

Verify the API is ready:

```bash
curl http://localhost:8080/healthz/ready   # substitute the URL from the dashboard
```

## Running without Aspire

If you prefer to run the API directly against your own MongoDB instance — for example, when contributing and debugging a single project — set the required environment variables and run `dotnet run`:

```bash
export ConnectionStrings__Storage="mongodb://localhost:27017"
export Persistence__MongoDb__DatabaseName="groundcontrol"
export Authentication__Mode="None"

dotnet run --project src/GroundControl.Api
```

Or use `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Storage": "mongodb://localhost:27017"
  },
  "Persistence": {
    "MongoDb": {
      "DatabaseName": "groundcontrol"
    }
  },
  "Authentication": {
    "Mode": "None"
  }
}
```

## MongoDB topology

The local Aspire setup runs MongoDB as a **standalone** instance paired with the `InProcess` change notifier, so no replica set is required for development.

Switching the server to `ChangeNotifier:Mode = MongoChangeStream` (needed for running multiple API instances against a shared database) will require MongoDB to be configured as a replica set, since change streams depend on the oplog. Detailed guidance for that topology will land with the production-deployment guide.

## Health checks

GroundControl exposes two health endpoints that are useful during local development and will remain the contract for production probes:

| Endpoint | Purpose | Checks |
|---|---|---|
| `GET /healthz/liveness` | Process is running | None (always returns 200) |
| `GET /healthz/ready` | Ready to serve requests | MongoDB connectivity, change notifier |

## What's next?

- [Configuration](configuration.md) — all server settings
- [Authentication](authentication.md) — authentication modes (set to `None` by the local AppHost)
