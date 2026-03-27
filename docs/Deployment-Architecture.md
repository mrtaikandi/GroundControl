# Deployment Architecture

This document describes the deployment models, scaling strategies, and infrastructure patterns for GroundControl.

---

## Deployment Models

GroundControl supports three deployment scenarios, from a single-container homelab setup to multi-region Kubernetes clusters. The same application binary is used in all scenarios; behavior differences are controlled by configuration.

### 1. Homelab / Single Instance

The simplest deployment: one GroundControl API container and one MongoDB container.

```
┌──────────────────────────────────┐
│           Docker Host             │
│                                   │
│  ┌───────────────┐  ┌──────────┐ │
│  │ GroundControl │  │ MongoDB  │ │
│  │     API       │←→│ (replica │ │
│  │               │  │   set)   │ │
│  └───────┬───────┘  └──────────┘ │
│          │                        │
└──────────┼────────────────────────┘
           │ :8080
     ┌─────┴─────┐
     │  Clients   │
     └────────────┘
```

**Characteristics:**
- Single API instance — no cross-instance coordination needed.
- Change notification uses `InProcessChangeNotifier` (in-memory channel).
- MongoDB runs as a single-node replica set (required for change stream capability, but no replication overhead).
- No load balancer required.
- Suitable for personal use, small teams, or development environments.

**Docker Compose quick start:**

```bash
docker compose up
```

The repository ships a `docker-compose.yml` at the solution root that starts MongoDB (single-node replica set) and the GroundControl API. MongoDB is initialized with `infra/mongo-init.js`, which calls `rs.initiate()` on first startup to enable change stream support.

```yaml
services:
  mongodb:
    image: mongo:8
    command: ["mongod", "--replSet", "rs0", "--bind_ip_all"]
    volumes:
      - mongo_data:/data/db
      - ./infra/mongo-init.js:/docker-entrypoint-initdb.d/mongo-init.js:ro
    healthcheck:
      test: ["CMD", "mongosh", "--eval", "db.adminCommand('ping')"]

  api:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - dp_keys:/keys
    environment:
      ConnectionStrings__Storage: "mongodb://mongodb:27017/?replicaSet=rs0"
      Persistence__MongoDb__DatabaseName: "groundcontrol"
      GroundControl__Security__AuthenticationMode: "None"
      DataProtection__Mode: "FileSystem"
      DataProtection__KeyStorePath: "/keys"
      ChangeNotifier__Mode: "InProcess"
    depends_on:
      mongodb:
        condition: service_healthy

volumes:
  mongo_data:
  dp_keys:
```

The API container runs as a non-root user and exposes port 8080. Health endpoints are available at `/healthz/liveness` and `/healthz/ready`. For production use, change `GroundControl__Security__AuthenticationMode` from `None` to `BuiltIn` or `External` and add a TLS-terminating reverse proxy in front.

On Linux, consider `DataProtection__Mode=Certificate` with a self-signed `.pfx` for key ring protection.

---

### 2. Kubernetes / Single Region

Multiple GroundControl API pods behind a service, backed by a MongoDB replica set.

```
┌──────────────────────────────────────────────────┐
│                 Kubernetes Cluster                 │
│                                                    │
│  ┌──────────────────────────────────┐              │
│  │        Service / Ingress          │             │
│  └──────┬──────────┬──────────┬─────┘             │
│         │          │          │                     │
│  ┌──────┴───┐ ┌────┴─────┐ ┌─┴────────┐          │
│  │ API Pod  │ │ API Pod  │ │ API Pod   │          │
│  │    #1    │ │    #2    │ │    #3     │          │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘          │
│       │             │            │                 │
│       └─────────────┼────────────┘                 │
│                     │                              │
│              ┌──────┴──────┐                       │
│              │   MongoDB   │                       │
│              │ Replica Set │                       │
│              └─────────────┘                       │
│                                                    │
└────────────────────────────────────────────────────┘
```

**Characteristics:**
- Multiple API pods for horizontal scaling and high availability.
- MongoDB replica set (3+ members) for data durability.
- Change notification uses `MongoChangeStreamNotifier` to propagate snapshot activations across pods.
- Kubernetes Service provides load balancing across pods.
- Ingress controller handles TLS termination.
- Horizontal Pod Autoscaler (HPA) scales based on CPU/memory or custom metrics (e.g., SSE connection count).

**Key configuration:**

```yaml
environment:
  - ChangeNotifier__Mode=MongoChangeStream
  - ConnectionStrings__Storage=mongodb://mongo-0,mongo-1,mongo-2:27017/?replicaSet=rs0
  - Persistence__MongoDb__DatabaseName=groundcontrol
  - DataProtection__Mode=Certificate
  - DataProtection__KeyStorePath=/app/keys
  - DataProtection__CertificateProvider=FileSystem
  - DataProtection__Certificate__FileSystem__CurrentPath=/certs/dp-current.pfx
  - DataProtection__Certificate__FileSystem__Password=<from-k8s-secret>
  - DataProtection__Certificate__FileSystem__PreviousPaths__0=/certs/dp-previous.pfx  # only during rotation
```

The Data Protection key ring directory (`/app/keys`) should be a shared volume (e.g., PersistentVolumeClaim with `ReadWriteMany`) or replaced with Redis storage (`DataProtection__Mode=Redis`). The certificate `.pfx` files are mounted from Kubernetes Secrets. For cloud deployments, consider using `DataProtection__CertificateProvider=AzureBlob` to source certificates from Azure Blob Storage instead of local files.

---

### 3. Multi-Region

Multiple Kubernetes clusters across regions, each with their own GroundControl API pods, backed by a shared MongoDB global cluster.

```
┌──────────────────────┐      ┌──────────────────────┐
│    Region: US-East    │      │    Region: EU-West    │
│                       │      │                       │
│ ┌───────────────────┐ │      │ ┌───────────────────┐ │
│ │    K8s Cluster     │ │      │ │    K8s Cluster     │ │
│ │                    │ │      │ │                    │ │
│ │ ┌──────┐ ┌──────┐ │ │      │ │ ┌──────┐ ┌──────┐ │ │
│ │ │ Pod  │ │ Pod  │ │ │      │ │ │ Pod  │ │ Pod  │ │ │
│ │ │  #1  │ │  #2  │ │ │      │ │ │  #1  │ │  #2  │ │ │
│ │ └──┬───┘ └──┬───┘ │ │      │ │ └──┬───┘ └──┬───┘ │ │
│ │    └────┬───┘     │ │      │ │    └────┬───┘     │ │
│ │         │         │ │      │ │         │         │ │
│ │  ┌──────┴──────┐  │ │      │ │  ┌──────┴──────┐  │ │
│ │  │  MongoDB    │  │ │      │ │  │  MongoDB    │  │ │
│ │  │  (Primary)  │←─┼─┼──────┼─┼─→│ (Secondary) │  │ │
│ │  └─────────────┘  │ │      │ │  └─────────────┘  │ │
│ │                    │ │      │ │                    │ │
│ └────────────────────┘ │      │ └────────────────────┘ │
└────────────────────────┘      └────────────────────────┘
```

**Characteristics:**
- Admin writes go to the MongoDB primary (in one region). Reads can be served from local secondaries.
- MongoDB replication handles data propagation across regions.
- Each region's GroundControl pods watch MongoDB Change Streams on their local secondary. When a snapshot activation replicates to the secondary, the change stream fires and pods push updates to their SSE clients.
- Cross-region latency for config updates equals MongoDB replication lag (typically sub-second to a few seconds).
- Clients connect to the nearest region via DNS-based routing (e.g., GeoDNS or cloud load balancer).

**Key configuration per region:**

```yaml
environment:
  - ChangeNotifier__Mode=MongoChangeStream
  - ConnectionStrings__Storage=mongodb+srv://cluster.example.com/?readPreference=secondaryPreferred
  - Persistence__MongoDb__DatabaseName=groundcontrol
  - DataProtection__Mode=Azure
  - DataProtection__Azure__BlobStorageUri=https://<account>.blob.core.windows.net/data-protection/keys.xml
  - DataProtection__Azure__KeyVaultKeyUri=https://<vault>.vault.azure.net/keys/groundcontrol-dp
```

Azure Key Vault handles key ring protection natively — no manual certificate management is needed. Use `DefaultAzureCredential` (Managed Identity in AKS) for authentication to both Blob Storage and Key Vault. All regions share the same blob container and Key Vault key, ensuring a consistent key ring.

---

## Pluggable Change Notification

The change notification system determines how GroundControl API instances learn about new snapshot activations so they can push updates to connected SSE clients.

### Interface

```
IChangeNotifier
├── NotifyAsync(projectId, snapshotId)     // Called when a snapshot is activated
├── SubscribeAsync() → IAsyncEnumerable    // Returns a stream of change events
└── Dispose()                              // Clean up resources
```

### Implementations

| Implementation | When to Use | Configuration |
|---------------|-------------|---------------|
| `InProcessChangeNotifier` | Single instance (homelab) | `ChangeNotifier:Mode = InProcess` |
| `MongoChangeStreamNotifier` | Multi-instance (K8s, multi-region) | `ChangeNotifier:Mode = MongoChangeStream` |
| *Future: Redis* | High-frequency, low-latency scenarios | `ChangeNotifier:Mode = Redis` |

### InProcess Notifier

Uses a `System.Threading.Channels.Channel<T>` as an in-memory pub/sub bus within the same process. Zero external dependencies.

### MongoDB Change Stream Notifier

Watches the `projects` collection for changes to the `activeSnapshotId` field. When a change is detected, it emits a notification to the local subscribers.

**Behavior:**
- Opens a change stream with a resume token for reliability.
- If the change stream is interrupted, it resumes from the last known token.
- Filters for `update` operations where `activeSnapshotId` has changed.

---

## SSE Connection Management

### Stateless Design

SSE connections are long-lived but the server design is stateless:
- Any API instance can serve any client.
- Client identity is derived from the API key on each connection.
- No session affinity / sticky sessions required.
- If a pod dies, clients reconnect to any available pod.

### Connection Scaling

Each SSE connection consumes a long-lived HTTP connection. For planning:

| Metric | Guideline |
|--------|-----------|
| Connections per pod | Depends on pod resources. A typical .NET Kestrel instance can handle 10,000+ concurrent connections. |
| Memory per connection | Minimal (~few KB per connection for the SSE writer) |
| Scaling trigger | Monitor active SSE connection count. Scale when nearing pod capacity. |

### Graceful Shutdown

When a Kubernetes pod receives a termination signal:

1. Stop accepting new SSE connections.
2. Send a final `event: shutdown` to all connected SSE clients (optional, as a hint to reconnect).
3. Close all SSE connections.
4. Complete any in-flight REST requests.
5. Shut down.

The client SDK handles reconnection automatically.

---

## Health Probes

| Probe | Path | Checks |
|-------|------|--------|
| Liveness | `/healthz/liveness` | Process is running |
| Readiness | `/healthz/ready` | MongoDB connection is healthy, change notifier is subscribed |

Kubernetes configuration:

```yaml
livenessProbe:
  httpGet:
    path: /healthz/liveness
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /healthz/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5

startupProbe:
  httpGet:
    path: /healthz/liveness
    port: 8080
  failureThreshold: 30
  periodSeconds: 2
```

---

## In-Memory Snapshot Cache

Each API instance maintains an in-memory cache of active snapshots to serve requests without hitting MongoDB on every request.

### Cache Behavior

| Event | Action |
|-------|--------|
| Pod startup | Cache is empty. Snapshots are loaded lazily on first request. |
| SSE client connects | Load snapshot into cache if not already cached, then serve |
| REST config request | Load snapshot into cache if not already cached, then serve (with ETag/304 support) |
| Snapshot activation (via change notifier) | Load the new snapshot into cache, evict the old one |

**Optional prewarm:** For latency-sensitive deployments, configure `Cache:PrewarmOnStartup = true` to eagerly load active snapshots for all projects at startup. This trades startup time and memory for guaranteed cache hits on the first request. Default is `false` (lazy loading).

### Cache Invalidation

The change notifier is the sole trigger for cache invalidation. When a notification arrives:

1. Fetch the new snapshot from MongoDB.
2. Resolve per-scope configurations for all connected clients of that project.
3. Update the cache.
4. Push the resolved config to SSE clients.

This ensures the cache is always consistent with the database, with a small propagation delay determined by the change notifier.

---

## Configuration Reference

Application configuration for GroundControl server:

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Storage` | *required* | MongoDB connection string, for example `mongodb://localhost:27017/?replicaSet=rs0` |
| `Persistence:MongoDb:DatabaseName` | `GroundControl` | MongoDB database name |
| `ChangeNotifier:Mode` | `InProcess` | `InProcess` or `MongoChangeStream` |
| `DataProtection:Mode` | `FileSystem` | Key ring configurator: `FileSystem`, `Certificate`, `Redis`, `Azure` |
| `DataProtection:KeyStorePath` | `./keys` | File system path for key ring storage (`FileSystem`, `Certificate`) |
| `DataProtection:UseDpapi` | `false` | Use DPAPI for key protection (`FileSystem` on Windows only) |
| `DataProtection:CertificateProvider` | `FileSystem` | Certificate provider: `FileSystem`, `AzureBlob` |
| `DataProtection:Certificate:FileSystem:CurrentPath` | — | Current X.509 certificate path (`.pfx`) |
| `DataProtection:Certificate:FileSystem:Password` | — | Certificate password (prefer environment variable or secrets manager) |
| `DataProtection:Certificate:FileSystem:PreviousPaths` | `[]` | Previous certificate paths for decrypting old keys during rotation |
| `DataProtection:Certificate:AzureBlob:ContainerUri` | — | Azure Blob Storage container URI for certificates |
| `DataProtection:Certificate:AzureBlob:CurrentBlobName` | — | Blob name of the current certificate |
| `DataProtection:Certificate:AzureBlob:PreviousBlobNames` | `[]` | Blob names of previous certificates |
| `DataProtection:Certificate:AzureBlob:Password` | — | Certificate password (shared across blobs) |
| `DataProtection:Redis:ConnectionString` | — | Redis connection string (`Redis` configurator) |
| `DataProtection:Redis:KeyName` | `GroundControl-DP-Keys` | Redis key name for key ring storage |
| `DataProtection:Azure:BlobStorageUri` | — | Azure Blob Storage URI for key ring storage |
| `DataProtection:Azure:KeyVaultKeyUri` | — | Azure Key Vault key URI for key ring protection |
| `DataProtection:KeyRotation:Enabled` | `false` | Enable automatic key rotation |
| `DataProtection:KeyRotation:KeyLifetime` | `90` | Days before a new key is generated (when rotation is enabled) |
| `Cache:PrewarmOnStartup` | `false` | Load all active snapshots into cache at startup (trades memory for guaranteed first-request cache hits) |
| `Snapshots:RetentionCount` | `50` | Number of snapshots to retain per project |
| `Clients:CleanupGracePeriodDays` | `30` | Days after deactivation before hard-deleting client keys |
| `Clients:CleanupInterval` | `1.00:00:00` | How often the cleanup background service runs |
| `Sse:HeartbeatInterval` | `00:00:30` | SSE heartbeat interval |
| `Audit:Store` | `MongoDB` | Audit store implementation |

---

## Observability

### Metrics (OpenTelemetry)

| Metric | Type | Description |
|--------|------|-------------|
| `groundcontrol.sse.connections.active` | Gauge | Current active SSE connections |
| `groundcontrol.sse.connections.total` | Counter | Total SSE connections (lifetime) |
| `groundcontrol.api.requests.total` | Counter | API requests by endpoint and status |
| `groundcontrol.api.requests.duration` | Histogram | API request duration |
| `groundcontrol.snapshots.published.total` | Counter | Snapshots published |
| `groundcontrol.snapshots.activated.total` | Counter | Snapshots activated (includes rollbacks) |
| `groundcontrol.cache.hits` | Counter | Snapshot cache hits |
| `groundcontrol.cache.misses` | Counter | Snapshot cache misses |
| `groundcontrol.changenotifier.events` | Counter | Change notification events received |

### Structured Logging

Use Serilog or the built-in .NET logging with structured log properties:

```
[INF] Snapshot published {ProjectId=abc, Version=12, EntryCount=47, Duration=234ms}
[INF] SSE client connected {ProjectId=abc, Scopes={environment=Production,region=EU}, RemoteIp=10.0.0.5}
[WRN] Change stream disconnected, reconnecting {ResumeToken=..., Attempt=3}
```

### Distributed Tracing

OpenTelemetry tracing for request flows:

- Management API requests
- Snapshot publish pipeline (merge → interpolate → encrypt → store → notify)
- Client config resolution (authenticate → resolve scope → serve from cache)
- Change notification propagation
