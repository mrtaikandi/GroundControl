# Getting Started

This guide walks you through setting up a GroundControl server, creating your first configuration, and connecting a .NET application. The entire process takes under 10 minutes.

```mermaid
graph LR
    A[Start Server] --> B[Create Scope]
    B --> C[Create Group + Project]
    C --> D[Add Config Entries]
    D --> E[Publish Snapshot]
    E --> F[Create Client]
    F --> G[Connect SDK]
```

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose installed
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for the client application)

## Step 1: Start the server

Create a `docker-compose.yml` file with the following content:

```yaml
name: groundcontrol

services:
  mongodb:
    image: mongo:8
    command: ["mongod", "--replSet", "rs0", "--bind_ip_all"]
    volumes:
      - mongo_data:/data/db
      - ./infra/mongo-init.js:/docker-entrypoint-initdb.d/mongo-init.js:ro
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

Start the server and verify it is healthy:

```bash
docker compose up -d
curl http://localhost:8080/healthz/ready
```

You should receive an HTTP 200 response once the server is ready.

> **Note:** Auth is set to `None` for this quickstart, which means all requests run as a built-in admin. See [Authentication](server/authentication.md) for production setup.

## Step 2: Create a scope

A scope defines a dimension that your configuration varies by, such as environment, region, or tenant. Create an "Environment" scope with three allowed values:

```bash
curl -X POST http://localhost:8080/api/scopes \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "dimension": "Environment",
    "allowedValues": ["dev", "staging", "prod"],
    "description": "Deployment environment"
  }'
```

Note the `id` in the response -- you will need it later.

## Step 3: Create a group and project

Groups organize related projects. Create a group, then create a project within it:

```bash
# Create a group
curl -X POST http://localhost:8080/api/groups \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{"name": "Platform Team", "description": "Platform engineering"}'

# Create a project (use the group ID from above)
curl -X POST http://localhost:8080/api/projects \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{"name": "My Web App", "description": "Customer-facing web application", "groupId": "GROUP_ID_HERE"}'
```

Replace `GROUP_ID_HERE` with the `id` returned from the group creation response.

## Step 4: Add a configuration entry

Create a configuration entry with a default value and environment-specific overrides:

```bash
# Use the project ID from above as ownerId
curl -X POST http://localhost:8080/api/config-entries \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "key": "App:LogLevel",
    "valueType": "String",
    "ownerId": "PROJECT_ID_HERE",
    "ownerType": "Project",
    "description": "Application log level",
    "values": [
      {"value": "Information"},
      {"scopes": {"Environment": "dev"}, "value": "Debug"},
      {"scopes": {"Environment": "prod"}, "value": "Warning"}
    ]
  }'
```

The first value (with no scopes) is the default. The scoped values override it when a client matches that specific environment. In this example, `dev` clients receive `Debug`, `prod` clients receive `Warning`, and everything else falls back to `Information`.

## Step 5: Publish a snapshot

Publishing resolves all configuration entries, interpolates any variables, and produces an immutable snapshot. Clients always receive configuration from the active snapshot.

```bash
curl -X POST http://localhost:8080/api/projects/PROJECT_ID_HERE/snapshots \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{"description": "Initial configuration"}'
```

Replace `PROJECT_ID_HERE` with your project ID.

## Step 6: Create a client

A client represents a specific application instance with fixed scope values. The server resolves the correct configuration for each client based on its scopes.

```bash
curl -X POST http://localhost:8080/api/projects/PROJECT_ID_HERE/clients \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "my-web-app-dev",
    "scopes": {"Environment": "dev"}
  }'
```

The response includes a `clientId` and `clientSecret`. Save both values -- the secret is only shown once. This client will receive configuration resolved for the `dev` environment.

## Step 7: Connect your .NET app

Create a new web application and install the GroundControl client SDK:

```bash
dotnet new web -n MyWebApp
cd MyWebApp
dotnet add package GroundControl.Link
```

Replace the contents of `Program.cs` with:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddGroundControl(options =>
{
    options.ServerUrl = "http://localhost:8080";
    options.ClientId = "CLIENT_ID_HERE";
    options.ClientSecret = "CLIENT_SECRET_HERE";
});

var app = builder.Build();

app.MapGet("/", (IConfiguration config) =>
    new { LogLevel = config["App:LogLevel"] });

app.Run();
```

Replace `CLIENT_ID_HERE` and `CLIENT_SECRET_HERE` with the values from Step 6.

Run the application and verify the configuration is loaded:

```bash
dotnet run
```

Visit the root endpoint. You should see:

```json
{"logLevel": "Debug"}
```

The value is `Debug` because the client was created with `Environment: dev`, and the scoped value for `dev` overrides the default.

## What's next?

- [Concepts](concepts.md) -- understand the full domain model
- [SDK Quickstart](sdk/quickstart.md) -- deeper SDK integration guide
- [Server Deployment](server/deployment.md) -- production deployment
- [CLI Reference](../cli/README.md) -- manage configuration from the command line
