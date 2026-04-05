# Deploying GroundControl

## Prerequisites

- MongoDB 6+ configured as a replica set (required for change streams)
- Docker (recommended) or .NET 10 runtime

## Docker Compose (recommended)

This is the fastest way to run GroundControl with MongoDB.

Create a `docker-compose.yml`:

```yaml
name: groundcontrol

services:
  mongodb:
    image: mongo:8
    command: ["mongod", "--replSet", "rs0", "--bind_ip_all"]
    volumes:
      - mongo_data:/data/db
      - ./build/mongo-init.js:/docker-entrypoint-initdb.d/mongo-init.js:ro
    healthcheck:
      test: ["CMD", "mongosh", "--eval", "try { rs.status().ok } catch(e) { quit(1) }"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - groundcontrol_net

  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    volumes:
      - dp_keys:/keys
    environment:
      ConnectionStrings__Storage: "mongodb://mongodb:27017/?replicaSet=rs0"
      Persistence__MongoDb__DatabaseName: "groundcontrol"
      Authentication__Mode: "None"
      DataProtection__Mode: "FileSystem"
      DataProtection__KeyStorePath: "/keys"
      ChangeNotifier__Mode: "InProcess"
    depends_on:
      mongodb:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/healthz/ready || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 20s
    networks:
      - groundcontrol_net

volumes:
  mongo_data:
  dp_keys:

networks:
  groundcontrol_net:
    driver: bridge
```

Start:

```bash
docker compose up -d
```

Verify:

```bash
curl http://localhost:8080/healthz/ready
```

> **Note:** The `dp_keys` volume persists Data Protection keys across container restarts. Losing these keys means encrypted values (sensitive config, auth tokens) become unreadable.

## Running without Docker

If you prefer running GroundControl directly:

```bash
# Set environment variables
export ConnectionStrings__Storage="mongodb://localhost:27017/?replicaSet=rs0"
export Persistence__MongoDb__DatabaseName="groundcontrol"
export Authentication__Mode="None"

# Run the server
dotnet run --project src/GroundControl.Api
```

Or use `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Storage": "mongodb://localhost:27017/?replicaSet=rs0"
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

## MongoDB replica set requirement

GroundControl requires MongoDB to run as a replica set, even in single-node deployments. This is because the change notification system uses MongoDB change streams to detect snapshot activations and push updates to connected clients in real time.

For a single-node replica set (development or homelab):

```javascript
// Connect to mongosh and initialize
rs.initiate({ _id: "rs0", members: [{ _id: 0, host: "localhost:27017" }] })
```

The Docker Compose example handles this automatically with the `mongo-init.js` script.

## Health checks

GroundControl exposes two health endpoints:

| Endpoint | Purpose | Checks |
|---|---|---|
| `GET /healthz/liveness` | Process is running | None (always returns 200) |
| `GET /healthz/ready` | Ready to serve requests | MongoDB connectivity, change notifier |

Use these for container orchestration:

- **Liveness probe:** `/healthz/liveness` — restart the container if this fails
- **Readiness probe:** `/healthz/ready` — remove from load balancer if this fails

## Scaling to multiple instances

For multi-instance deployments (Kubernetes, multiple Docker containers behind a load balancer):

1. Set `ChangeNotifier__Mode` to `MongoChangeStream` — this uses MongoDB change streams so all instances detect snapshot activations, not just the one that published
2. Use a shared Data Protection key store (Redis, Azure Blob, or a shared file system) so all instances can encrypt/decrypt consistently
3. Point all instances at the same MongoDB replica set

See [Configuration](configuration.md) for all server settings.

## Deployment models

| Model | Instances | Change Notifier | Data Protection | Use case |
|---|---|---|---|---|
| Homelab | 1 | `InProcess` | `FileSystem` | Development, personal projects |
| Kubernetes | N | `MongoChangeStream` | `Redis` or `Azure` | Production, team use |
| Multi-region | N per region | `MongoChangeStream` | `Azure` | Global deployments |

## What's next?

- [Configuration](configuration.md) — all server settings
- [Authentication](authentication.md) — set up auth for production
