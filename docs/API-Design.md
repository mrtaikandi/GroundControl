# API Design

This document defines the RESTful API surface for GroundControl, covering both the Management API (for admins) and the Client API (for consuming applications).

See [Domain Model](Domain-Model.md) for entity definitions, [Security Model](Security-Model.md) for authentication and authorization details.

---

## API Conventions

### Base URLs

| API | Base Path | Authentication |
|-----|-----------|----------------|
| Management API | `/api/` | Pluggable: None, Built-In (cookie/JWT), or External OIDC. See [Authentication & Authorization](Authentication-Authorization.md). |
| Auth Endpoints | `/auth/` | Varies by endpoint (see below) |
| Client API | `/client/` | `clientId` + `clientSecret` (except `/client/health`, which is unauthenticated) |

### Versioning

API versioning uses the `api-version` HTTP request header, implemented via the `Asp.Versioning` library with `HeaderApiVersionReader("api-version")`.

**Request:**
```
GET /api/projects HTTP/1.1
api-version: 1.0
```

**Defaults:**
- When the `api-version` header is omitted, the server assumes version `1.0` (`AssumeDefaultVersionWhenUnspecified = true`).

**Response headers** (enabled via `ReportApiVersions = true`):
```
api-supported-versions: 1.0
api-deprecated-versions: <none until a version is deprecated>
```

When a version is deprecated, clients receive the `api-deprecated-versions` header to signal they should migrate.

### Request/Response Format

- **Content-Type**: `application/json` for all REST requests and responses. The SSE endpoint (`/client/config/stream`) uses `text/event-stream`.
- **Date format**: ISO 8601 (`2024-01-15T10:30:00Z`).
- **IDs**: UUIDv7 GUIDs (e.g., `0192d4e0-7b3a-7f2e-8a1c-4d5e6f7a8b9c`), generated server-side via `Guid.CreateVersion7()`. Naturally sortable by creation time.

### Pagination (Cursor-Based)

List endpoints use cursor-based pagination for consistent results even when data changes between pages.

**Request parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `limit` | int | 25 | Number of items per page (max 100) |
| `after` | string? | null | Opaque cursor from a previous response; requests the next page after that boundary item |
| `before` | string? | null | Opaque cursor from a previous response; requests the previous page before that boundary item |
| `sort` | string? | `name` | Field to sort by |
| `order` | string? | `asc` | Sort direction: `asc` or `desc` |

`after` and `before` are mutually exclusive. Cursors are opaque to clients and are only valid when reused with the same endpoint, filter set, and sort settings that produced them.

**Response envelope:**

```json
{
  "data": [...],
  "nextCursor": "eyJpZCI6IjY1YTEuLi4ifQ==",
  "previousCursor": null,
  "totalCount": 142
}
```

The public JSON contract is flattened: list responses expose `nextCursor`, `previousCursor`, and `totalCount` at the top level rather than inside a nested `pagination` object.

Server-side C# helpers may expose computed `HasNext` / `HasPrevious` properties for convenience, but those flags are computed from cursor presence and are not part of the JSON contract.

### Filtering

List endpoints support filtering via query parameters:

```
GET /api/config-entries?ownerId=<id>&ownerType=template&key=Logging:*
```

- Exact match: `?field=value`
- Pattern match (where supported): `?key=Logging:*` (prefix matching on config keys)
- Multiple values: `?status=active,inactive`

### Search

Endpoints that support text search accept a `q` parameter:

```
GET /api/projects?q=payment
```

### Error Responses

All errors use the [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details format, produced automatically by ASP.NET Core's built-in `ProblemDetails` middleware.

**Content-Type:** `application/problem+json`

**Example — validation error:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "name": ["Name is required"],
    "dimension": ["Dimension must be unique"]
  }
}
```

**Example — business rule violation:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Template key overlap detected: Logging:LogLevel:Default"
}
```

**Standard HTTP status codes used:**

| HTTP Status | Usage |
|-------------|-------|
| 400 | Invalid request body or parameters (validation errors) |
| 401 | Missing or invalid authentication |
| 403 | Authenticated but insufficient permissions |
| 404 | Resource not found |
| 409 | Optimistic concurrency conflict or business rule violation |
| 422 | Request is valid but cannot be processed (e.g., unresolved variable references) |
| 429 | Too many requests |
| 500 | Unexpected server error |

### Optimistic Concurrency

Update and delete operations require the `If-Match` header with the entity's current version:

```
PUT /api/projects/{id}
If-Match: "5"
```

If the version doesn't match, the server returns `409 Conflict`.

Responses include the `ETag` header with the current version:

```
HTTP/1.1 200 OK
ETag: "6"
```

---

## Management API

Management API authentication is handled by a pluggable layer. See [Authentication & Authorization](Authentication-Authorization.md) for details on the three modes (None, Built-In, External) and the authorization matrix.

**Authorization annotations** below use permission names from the [hybrid permission model](Authentication-Authorization.md#permission-model). Permissions are checked against the user's effective permissions for the resource's group scope (system-wide grants serve as a global fallback).

- **`permission:name`** — user must have this permission (via any applicable grant)
- **Self** — the authenticated user is accessing their own resource
- **Authenticated** — any authenticated user

### Scopes

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/scopes` | List all scopes | `scopes:read` |
| POST | `/api/scopes` | Create a scope definition | `scopes:write` |
| GET | `/api/scopes/{id}` | Get a scope definition | `scopes:read` |
| PUT | `/api/scopes/{id}` | Update a scope definition | `scopes:write` |
| DELETE | `/api/scopes/{id}` | Delete a scope definition | `scopes:write` |

**POST/PUT Request:**
```json
{
  "dimension": "environment",
  "allowedValues": ["Production", "Staging", "Development"],
  "description": "Deployment environment"
}
```

**GET Response:**
```json
{
  "id": "65a1...",
  "dimension": "environment",
  "allowedValues": ["Production", "Staging", "Development"],
  "description": "Deployment environment",
  "version": 1,
  "createdAt": "2024-01-15T10:30:00Z",
  "createdBy": "user-id",
  "updatedAt": "2024-01-15T10:30:00Z",
  "updatedBy": "user-id"
}
```

---

### Groups

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/groups` | List groups | `groups:read` |
| POST | `/api/groups` | Create a group | `groups:write` |
| GET | `/api/groups/{id}` | Get a group | `groups:read` |
| PUT | `/api/groups/{id}` | Update a group | `groups:write` |
| DELETE | `/api/groups/{id}` | Delete a group | `groups:write` |

#### Group Members

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/groups/{id}/members` | List users with grants in this group | `groups:write` |
| PUT | `/api/groups/{id}/members/{userId}` | Set a user's grant for this group | `groups:write` |
| DELETE | `/api/groups/{id}/members/{userId}` | Remove a user's grant for this group | `groups:write` |

---

### Projects

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/projects` | List projects (filtered by group if applicable) | `projects:read` |
| POST | `/api/projects` | Create a project | `projects:write` |
| GET | `/api/projects/{id}` | Get a project | `projects:read` |
| PUT | `/api/projects/{id}` | Update a project | `projects:write` |
| DELETE | `/api/projects/{id}` | Delete a project and all related data | `projects:write` |

**POST/PUT Request:**
```json
{
  "name": "payment-service",
  "description": "Payment processing microservice",
  "groupId": "65a1...",
  "templateIds": ["tmpl-1", "tmpl-2"]
}
```

**Validation on template assignment:**
- All referenced templates must exist and be accessible to the project's group.
- The combined keys across all assigned templates must not overlap.
- Returns `409 Conflict` with details if key overlap is detected.

#### Template Management on Projects

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| POST | `/api/projects/{id}/templates` | Add a template to the project | `projects:write` |
| DELETE | `/api/projects/{id}/templates/{templateId}` | Remove a template from the project | `projects:write` |

---

### Templates

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/templates` | List templates (filter by `groupId` or `global=true`) | `templates:read` |
| POST | `/api/templates` | Create a template | `templates:write` |
| GET | `/api/templates/{id}` | Get a template | `templates:read` |
| PUT | `/api/templates/{id}` | Update template metadata | `templates:write` |
| DELETE | `/api/templates/{id}` | Delete a template (only if not referenced by any project) | `templates:write` |

---

### Variables

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/variables` | List variables (filter by `scope`, `groupId`, `projectId`) | `variables:read` |
| POST | `/api/variables` | Create a variable | `variables:write` |
| GET | `/api/variables/{id}` | Get a variable | `variables:read` |
| PUT | `/api/variables/{id}` | Update a variable | `variables:write` |
| DELETE | `/api/variables/{id}` | Delete a variable (only if not referenced) | `variables:write` |

**Sensitive value handling:**
- By default, sensitive variable values are masked (`"***"`) in GET responses.
- To retrieve decrypted values, include the query parameter `?decrypt=true`. This requires the `sensitive_values:decrypt` permission in addition to `variables:read`.
- Every decrypted read is recorded in the audit log.

**POST/PUT Request:**
```json
{
  "name": "DatabaseHost",
  "description": "Database server hostname",
  "scope": "global",
  "groupId": null,
  "values": [
    {
      "scopes": {},
      "value": "localhost"
    },
    {
      "scopes": { "environment": "Production" },
      "value": "db-prod.example.com"
    },
    {
      "scopes": { "environment": "Production", "region": "EU" },
      "value": "db-prod-eu.example.com"
    }
  ],
  "isSensitive": false
}
```

---

### Configuration Entries

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/config-entries` | List entries (filter by `ownerId`, `ownerType`, `key` prefix) | `config-entries:read` |
| POST | `/api/config-entries` | Create a config entry | `config-entries:write` |
| GET | `/api/config-entries/{id}` | Get a config entry | `config-entries:read` |
| PUT | `/api/config-entries/{id}` | Update a config entry | `config-entries:write` |
| DELETE | `/api/config-entries/{id}` | Delete a config entry | `config-entries:write` |

**Sensitive value handling:**
- By default, sensitive config entry values are masked (`"***"`) in GET responses.
- To retrieve decrypted values, include the query parameter `?decrypt=true`. This requires the `sensitive_values:decrypt` permission in addition to `config-entries:read`.
- Every decrypted read is recorded in the audit log.

**POST/PUT Request:**
```json
{
  "key": "Database:ConnectionString",
  "ownerId": "project-id",
  "ownerType": "project",
  "valueType": "String",
  "values": [
    {
      "scopes": {},
      "value": "Server={{DatabaseHost}};Database=mydb;User={{DbUser}};Password={{DbPassword}}"
    },
    {
      "scopes": { "environment": "Development" },
      "value": "Server=localhost;Database=mydb_dev;Integrated Security=true"
    }
  ],
  "isSensitive": true,
  "description": "Primary database connection string"
}
```

**Validation:**
- `valueType` must be a supported .NET type.
- `value` is validated against `valueType` (after stripping `{{...}}` placeholders).
- Scope dimension names must reference valid scopes.
- Scope dimension values must be in the allowed values for that dimension.
- Key uniqueness is enforced within the owner.
- If a project-level entry has the same key as a template entry (from an attached template), the project entry acts as an explicit override during snapshot resolution.

---

### Snapshots

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/projects/{projectId}/snapshots` | List snapshots for a project | `snapshots:read` |
| POST | `/api/projects/{projectId}/snapshots` | Publish and activate a new snapshot | `snapshots:publish` |
| GET | `/api/projects/{projectId}/snapshots/{id}` | Get a specific snapshot | `snapshots:read` |
| POST | `/api/projects/{projectId}/snapshots/{id}/activate` | Activate (rollback to) a previous snapshot | `snapshots:publish` |

**Sensitive value handling:**
- Sensitive values in snapshot entries are masked (`"***"`) by default in GET responses.
- To retrieve decrypted values, include the query parameter `?decrypt=true`. This requires the `sensitive_values:decrypt` permission in addition to `snapshots:read`.
- Every decrypted read is recorded in the audit log.

**POST (Publish) Request:**
```json
{
  "description": "Release 2.4.0 configuration update"
}
```

**POST (Publish) Response (201):**
```json
{
  "id": "snap-id",
  "projectId": "project-id",
  "snapshotVersion": 12,
  "entryCount": 47,
  "publishedAt": "2024-01-15T10:30:00Z",
  "publishedBy": "user-id",
  "description": "Release 2.4.0 configuration update"
}
```

**Publish validation errors (422):**
- Unresolved variable references: lists the keys and unresolved `{{...}}` placeholders.
- Type validation failures: lists entries where the interpolated value doesn't match the declared type.

---

### Clients

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/projects/{projectId}/clients` | List clients for a project | `clients:read` |
| POST | `/api/projects/{projectId}/clients` | Create a new client | `clients:write` |
| GET | `/api/projects/{projectId}/clients/{id}` | Get client details (without the secret) | `clients:read` |
| PUT | `/api/projects/{projectId}/clients/{id}` | Update client metadata (name, isActive, expiresAt) | `clients:write` |
| DELETE | `/api/projects/{projectId}/clients/{id}` | Delete a client | `clients:write` |

**POST Request:**
```json
{
  "name": "payment-service-prod-eu",
  "scopes": {
    "environment": "Production",
    "region": "EU"
  },
  "expiresAt": "2025-01-15T00:00:00Z"
}
```

**POST Response (201):**
```json
{
  "id": "key-id",
  "name": "payment-service-prod-eu",
  "projectId": "project-id",
  "scopes": { "environment": "Production", "region": "EU" },
  "clientId": "key-id",
  "clientSecret": "a1b2c3d4e5f6g7h8...",
  "isActive": true,
  "expiresAt": "2025-01-15T00:00:00Z",
  "version": 1,
  "createdAt": "2024-01-15T10:30:00Z",
  "createdBy": "user-id",
  "updatedAt": "2024-01-15T10:30:00Z",
  "updatedBy": "user-id"
}
```

> **Note:** The `clientSecret` field is only present in the creation response. It is never returned again.

---

### Roles

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/roles` | List all roles | `roles:read` |
| POST | `/api/roles` | Create a custom role | `roles:write` |
| GET | `/api/roles/{id}` | Get role details and permissions | `roles:read` |
| PUT | `/api/roles/{id}` | Update a role (name, description, permissions) | `roles:write` |
| DELETE | `/api/roles/{id}` | Delete a role (fails if referenced by any user's grants) | `roles:write` |

**POST/PUT Request:**
```json
{
  "name": "DevOps",
  "description": "Can manage projects and publish snapshots",
  "permissions": ["projects:read", "projects:write", "snapshots:read", "snapshots:publish", "config-entries:read", "config-entries:write"]
}
```

**GET Response:**
```json
{
  "id": "role-id",
  "name": "DevOps",
  "description": "Can manage projects and publish snapshots",
  "permissions": ["projects:read", "projects:write", "snapshots:read", "snapshots:publish", "config-entries:read", "config-entries:write"],
  "version": 1,
  "createdAt": "2026-02-25T00:00:00Z",
  "createdBy": "user-id",
  "updatedAt": "2026-02-25T00:00:00Z",
  "updatedBy": "user-id"
}
```

**Validation:**
- `name` must be unique.
- All strings in `permissions` must be valid permission constants (see [Authentication & Authorization](Authentication-Authorization.md#permissions)).
- Delete returns `409 Conflict` if the role is referenced by any user's grants.

---

### Users

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/users` | List users | `users:read` |
| POST | `/api/users` | Create a user | `users:write` |
| GET | `/api/users/{id}` | Get a user | `users:read` or Self |
| PUT | `/api/users/{id}` | Update a user | `users:write` (grants, isActive), Self (username, email) |
| DELETE | `/api/users/{id}` | Delete a user | `users:write` |
| PUT | `/api/users/{id}/password` | Change password (built-in auth only) | `users:write` or Self |

User GET responses include `version` and `ETag`. PUT and DELETE on `/api/users/{id}` require the `If-Match` header (standard optimistic concurrency). The password change endpoint (`PUT /api/users/{id}/password`) does not require `If-Match` — it validates the current password instead.

#### Personal Access Tokens

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/users/{userId}/tokens` | List user's PATs (metadata only) | Self or `users:write` |
| POST | `/api/users/{userId}/tokens` | Create a PAT (returns raw token once) | Self only |
| GET | `/api/users/{userId}/tokens/{tokenId}` | Get PAT metadata | Self or `users:write` |
| DELETE | `/api/users/{userId}/tokens/{tokenId}` | Revoke a PAT | Self or `users:write` |

**POST Request:**
```json
{
  "name": "CI pipeline",
  "expiresInDays": 90,
  "permissions": ["config-entries:read", "snapshots:read"]
}
```

`permissions` is optional. When omitted or `null`, the PAT inherits all of the owning user's permissions. When provided, all values must be valid permission constants (from the 22 defined permissions). Invalid values return `400 Bad Request`.

**POST Response (201):**
```json
{
  "id": "0192d4e0-...",
  "name": "CI pipeline",
  "token": "gc_pat_a1b2c3d4e5f6...",
  "tokenPrefix": "gc_pat_a1",
  "permissions": ["config-entries:read", "snapshots:read"],
  "expiresAt": "2026-05-26T00:00:00Z",
  "createdAt": "2026-02-25T00:00:00Z"
}
```

> **Note:** The `token` field is only present in the creation response. It is never returned again. The `permissions` field is `null` when the PAT inherits full user permissions. PATs are available in Built-In and External authentication modes only.

---

### Audit Records

| Method | Path | Description | Authorization |
|--------|------|-------------|---------------|
| GET | `/api/audit-records` | List audit records (filter by `entityType`, `entityId`, `performedBy`, date range) | `audit:read` (scoped) |
| GET | `/api/audit-records/{id}` | Get a specific audit record | `audit:read` (scoped) |

Audit records are read-only. No create, update, or delete endpoints are exposed. Results are filtered by the user's grants — users only see audit records for groups they have access to, plus system-level records. Users with system-wide `audit:read` (no conditions) see all records. See [Authentication & Authorization](Authentication-Authorization.md#scoped-audit-records).

---

## Authentication Endpoints

Authentication endpoints are mode-dependent. See [Authentication & Authorization](Authentication-Authorization.md) for full details.

### Built-In Mode

| Method | Path | Description | Auth Required |
|--------|------|-------------|---------------|
| `POST` | `/auth/login` | Cookie-based login. Returns `Set-Cookie` header. | No (accepts credentials) |
| `POST` | `/auth/logout` | Clears session cookie. | Yes |
| `POST` | `/auth/token` | JWT-based login. Returns access token + refresh token. | No (accepts credentials) |
| `POST` | `/auth/token/refresh` | Exchanges refresh token for new access token + refresh token (rotation). | No (accepts refresh token) |
| `GET` | `/auth/me` | Returns current user info. | Yes |

### External OIDC Mode

| Method | Path | Description | Auth Required |
|--------|------|-------------|---------------|
| `GET` | `/auth/login/external` | Initiates OIDC challenge (redirects to IdP). | No |
| `GET` | `/auth/callback` | OIDC callback. Sets session cookie. | No (handles OIDC response) |
| `POST` | `/auth/logout` | Clears session + optional OIDC front-channel logout. | Yes |
| `GET` | `/auth/me` | Returns current user info. | Yes |

### No Auth Mode

No authentication endpoints are exposed. All requests are treated as a system user with full admin permissions.

---

## Client API

The Client API is used by client applications to receive configuration. It is authenticated via API keys.

### GET `/client/config`

Fetch the current resolved configuration for the authenticated client.

**Headers:**
```
Authorization: ApiKey <clientId>:<clientSecret>
api-version: 1.0
```

**Response (200):**
```json
{
  "projectId": "project-id",
  "projectName": "payment-service",
  "snapshotVersion": 12,
  "publishedAt": "2024-01-15T10:30:00Z",
  "entries": {
    "Logging:LogLevel:Default": {
      "value": "Warning",
      "valueType": "String"
    },
    "Database:ConnectionString": {
      "value": "Server=db-prod-eu.example.com;Database=mydb;User=svc_payment;Password=s3cret",
      "valueType": "String"
    },
    "FeatureFlags:NewCheckout": {
      "value": "true",
      "valueType": "Boolean"
    }
  }
}
```

> **Auth scheme rationale:** The `ApiKey` scheme is a custom HTTP authentication scheme (permitted by RFC 7235). It avoids `Basic` auth (which can trigger browser login dialogs and requires Base64 encoding) and `Bearer` (which is reserved for the Management API's JWT/PAT tokens). The scheme name is case-insensitive per HTTP spec.

**Notes:**
- The response contains only the entries resolved for the client's registered scope combination.
- Sensitive values are decrypted and included in plaintext (the connection is TLS-encrypted).
- If no active snapshot exists, returns `404 Not Found`.

**Response (304 Not Modified):**
If the client sends `If-None-Match: "12"` (the snapshot version) and the version hasn't changed, the server returns 304 with no body.

---

### GET `/client/config/stream`

Open an SSE connection for real-time configuration updates.

**Headers:**
```
Authorization: ApiKey <clientId>:<clientSecret>
api-version: 1.0
```

**SSE Event Format:**

On initial connection, the server sends the current config:
```
id: 12
event: config
data: {"snapshotVersion":12,"publishedAt":"2024-01-15T10:30:00Z","entries":{"Logging:LogLevel:Default":{"value":"Warning","valueType":"String"},...}}
```

On configuration change (new snapshot activated):
```
id: 13
event: config
data: {"snapshotVersion":13,"publishedAt":"2024-01-15T14:20:00Z","entries":{...}}
```

Periodic heartbeat to keep the connection alive:
```
event: heartbeat
data: {"timestamp":"2024-01-15T10:35:00Z"}
```

The `id` field on `config` events is set to the `snapshotVersion`. This enables the SSE `Last-Event-ID` reconnect protocol — on reconnect, the client sends `Last-Event-ID: <snapshotVersion>` and the server skips sending a `config` event if the active snapshot version matches (avoiding duplicate delivery). Heartbeat events intentionally omit `id` to avoid overwriting the client's last known snapshot version.

**SSE Event Types:**

| Event | Description |
|-------|-------------|
| `config` | Full resolved configuration payload (sent on connect and on each change) |
| `heartbeat` | Keep-alive signal (sent at a configurable interval, e.g., every 30 seconds) |

**Connection lifecycle:**
1. Client opens SSE connection with API key.
2. Server authenticates the key, resolves project + scopes.
3. Server sends the current config as the first `config` event.
4. Server keeps the connection open, sending `heartbeat` events periodically.
5. When a new snapshot is activated for the client's project, the server resolves the config for the client's scope and sends a `config` event.
6. If the client disconnects, it can reconnect using `Last-Event-ID` header (set to the last received `snapshotVersion`) to avoid receiving stale data.

---

### GET `/client/health`

Health check endpoint for client applications to verify connectivity.

**Response (200):**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

No authentication required.

---

## Management API Health Endpoints

### GET `/healthz/liveness`

Liveness probe. Returns 200 if the process is running.

**Response (200):**
```json
{
  "status": "healthy"
}
```

### GET `/healthz/ready`

Readiness probe. Returns 200 only when all dependencies are healthy. Returns 503 when any dependency is unhealthy.

**Dependency checks:**
- **MongoDB**: Connection is established and responsive.
- **Change notifier**: Subscription is active (MongoChangeStream cursor is open, or InProcess channel is ready).

**Response (200 — healthy):**
```json
{
  "status": "healthy",
  "checks": {
    "mongodb": "healthy",
    "changeNotifier": "healthy"
  }
}
```

**Response (503 — unhealthy):**
```json
{
  "status": "unhealthy",
  "checks": {
    "mongodb": "unhealthy",
    "changeNotifier": "healthy"
  },
  "reason": "MongoDB connection timeout"
}
```
