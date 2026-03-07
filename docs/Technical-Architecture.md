# GroundControl — Technical Architecture Overview

**GroundControl** is a centralized configuration management platform for modern distributed .NET applications. It enables organizations to manage, distribute, and monitor configuration data across multiple client applications with real-time updates, multi-dimensional scoping, and high availability.

---

## Goals

1. **Centralized Configuration Management** — Single source of truth for application configuration across distributed environments.
2. **Real-time Distribution** — Near-instant configuration updates via SSE without application restarts.
3. **Multi-dimensional Scoping** — Flexible key-value scope dimensions (environment, region, tier, etc.) with most-specific-match resolution.
4. **High Availability** — Horizontal scaling, multi-region redundancy, graceful degradation.
5. **Developer Experience** — Clean REST APIs, standard .NET `IConfigurationProvider` integration, intuitive domain model.
6. **Security** — RBAC, encrypted sensitive values, pluggable secret provider, API key authentication for clients.

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 10 / ASP.NET Core |
| Database | MongoDB (replica set) |
| Real-time delivery | Server-Sent Events (SSE) |
| Client integration | IConfigurationProvider (NuGet SDK) |
| Admin authentication | Pluggable: None, Built-In (ASP.NET Identity), or External (OIDC) |
| Client authentication | `clientId` + `clientSecret` per project + scope |
| Observability | OpenTelemetry (metrics, tracing, structured logging) |

---

## System Architecture

```
                    ┌─────────────────────────────────────────┐
                    │            Management API               │
                    │         (REST)                          │
                    │                                         │
                    │  Scopes · Groups · Projects · Templates │
                    │  Variables · Config Entries · Snapshots │
                    │  Clients · Users · Roles · Audit        │
                    └────────────────┬────────────────────────┘
                                     │
                                     │ reads/writes
                                     ▼
┌──────────────┐            ┌─────────────────┐            ┌──────────────────┐
│   Admin      │───REST────→│  GroundControl  │←──Change──→│    MongoDB       │
│   Users      │            │     Server      │  Streams   │  (replica set)   │
└──────────────┘            │                 │            └──────────────────┘
                            │  ┌───────────┐  │
                            │  │ Snapshot  │  │
                            │  │ Cache     │  │
                            │  └───────────┘  │
┌──────────────┐            │                 │
│   Client     │───SSE/────→│  Client API     │
│   Apps       │   REST     │  (API key auth) │
└──────────────┘            └─────────────────┘
```

---

## Core Domain Concepts

| Concept | Description |
|---------|-------------|
| **Project** | 1:1 mapping to a client application. Owns config entries, snapshots, and clients. |
| **Group** | Optional organizational unit for resource isolation. Owns projects, templates, and variables. |
| **Template** | Reusable set of scoped config entries. Projects inherit from templates and can override values. |
| **Variable** | Two-tier (global + project) named placeholder. Scope-aware. Interpolated as `{{name}}` in values. |
| **Scope** | Key-value dimension pair (e.g., `environment=Production`). Predefined at system level. Most-specific match wins. |
| **Config Entry** | Individual key with typed value, scope variants, and sensitivity flag. Uses colon hierarchy (`Logging:LogLevel:Default`). |
| **Snapshot** | Immutable, pre-computed artifact per project. Contains all entries with all scope variants, variables interpolated. Created by explicit publish. |
| **Client** | Tied to a project + scope combination. Determines what config a client receives. |

Details: [Domain Model](Domain-Model.md)

---

## Key Data Flows

### Admin: Modify and Publish Configuration

```
Admin edits config entries / templates / variables
       │
       ▼
Admin publishes snapshot for project
       │
       ▼
Server resolves: merge templates + project overrides
       │
       ▼
Server interpolates variables per scope variant
       │
       ▼
Server encrypts sensitive values
       │
       ▼
Immutable snapshot stored in MongoDB
       │
       ▼
Project's activeSnapshotId updated
       │
       ▼
Change notification fires (InProcess or Change Stream)
       │
       ▼
All server instances push new config to connected SSE clients
```

### Client: Receive Configuration

```
Client app starts
       │
       ▼
SDK connects to GroundControl server (SSE or REST)
       │
       ├── Success → receive resolved config for project + scope
       │              update local file cache
       │
       ├── Server unreachable → load from local file cache
       │
       └── No cache → fall back to appsettings.json

       (SDK keeps retrying server connection in background)
```

### Client: Real-time Update

```
New snapshot activated for project
       │
       ▼
Change notifier propagates to all server instances
       │
       ▼
Each instance resolves config for each connected client's scope
       │
       ▼
Full resolved config pushed via SSE "config" event
       │
       ▼
Client SDK updates IConfiguration, triggers IOptionsMonitor change tokens
       │
       ▼
Local file cache updated asynchronously
```

---

## Deployment Models

| Model | Instances | Change Notifier | MongoDB | Use Case |
|-------|-----------|----------------|---------|----------|
| **Homelab** | 1 API + 1 MongoDB | InProcess | Single-node replica set | Personal, small team, dev |
| **Kubernetes** | N API pods | MongoChangeStream | 3+ node replica set | Production, single region |
| **Multi-region** | N pods per region | MongoChangeStream | Global cluster | Enterprise, multi-region |

Details: [Deployment Architecture](Deployment-Architecture.md)

---

## Security Model

| Concern | Approach |
|---------|----------|
| Admin authentication | Pluggable via `IAuthConfigurator`: None (all-admin), Built-In (ASP.NET Identity + cookie/JWT), External (OIDC) |
| Client authentication | `clientId` + `clientSecret` per project + scope |
| Authorization | Hybrid permission model: 22 fine-grained permissions, database-stored roles, scoped grants with optional conditions. ASP.NET Core Authorization with resource-based handlers. |
| Personal access tokens | `gc_pat_*` bearer tokens for CI/CD and automation (BuiltIn and External modes only) |
| Sensitive values | ASP.NET Data Protection encryption at rest; opt-in key rotation; pluggable for external vaults |
| Transport | TLS required in production |
| Audit | Full diff trail (who, when, before/after) with pluggable store |

The Management API host selects the active admin authentication mode from `GroundControlOptions.Security.AuthenticationMode` under the `GroundControl` configuration root.

Details: [Security Model](Security-Model.md)

---

## Pluggable Subsystems

GroundControl uses interface-based abstractions for components that may need different implementations depending on deployment:

| Subsystem | Interface | Default | Alternatives |
|-----------|-----------|---------|-------------|
| Change notification | `IChangeNotifier` | InProcess (in-memory channel) | MongoChangeStream, Redis (future) |
| Audit store | `IAuditStore` | MongoDB (same database) | External database, event stream |
| Value protection | `IValueProtector` | ASP.NET Data Protection | Azure Key Vault, HashiCorp Vault |
| Key ring configuration | `IKeyRingConfigurator` | FileSystem (local, DPAPI) | Certificate, Redis, Azure, AWS (future) |
| Data protection certificate | `IDataProtectionCertificateProvider` | FileSystem (local `.pfx`) | AzureBlob, AzureKeyVault (future) |
| Authentication | `IAuthConfigurator` | None (all requests = admin) | Built-In (ASP.NET Identity), External (OIDC: Entra ID, Keycloak, OpenIddict) |

---

## Detailed Documentation

| Document | Description |
|----------|-------------|
| [Domain Model](Domain-Model.md) | Core entities, relationships, business rules, scope resolution algorithm |
| [Data Model](Data-Model.md) | MongoDB collections, schemas, indexes, concurrency, change streams |
| [Authentication & Authorization](Authentication-Authorization.md) | Pluggable auth modes, role hierarchy, RBAC, PATs, CSRF |
| [Security Model](Security-Model.md) | Data protection, encryption, API key lifecycle, transport security |
| [API Design](API-Design.md) | REST endpoints, SSE protocol, pagination, error handling |
| [Client SDK Design](Client-SDK-Design.md) | IConfigurationProvider, SSE client, fallback chain, caching |
| [Deployment Architecture](Deployment-Architecture.md) | Docker Compose, Kubernetes, multi-region, observability |
