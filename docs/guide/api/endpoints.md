# API Endpoints Reference

This page lists all GroundControl API endpoints grouped by domain. For conventions (pagination, ETags, errors), see [API Overview](overview.md). For CLI equivalents, see the [CLI Reference](../../cli/README.md).

---

## Scopes

Scopes define the dimensions your configuration varies by (e.g., Environment, Region).

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/scopes` | List scopes. |
| `POST` | `/api/scopes` | Create a scope. |
| `GET` | `/api/scopes/{id}` | Get a scope. |
| `PUT` | `/api/scopes/{id}` | Update a scope. Requires `If-Match`. |
| `DELETE` | `/api/scopes/{id}` | Delete a scope. Requires `If-Match`. |

**Create a scope:**

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

Response:

```json
{
  "id": "019abc12-...",
  "dimension": "Environment",
  "allowedValues": ["dev", "staging", "prod"],
  "description": "Deployment environment",
  "version": 1,
  "createdAt": "2025-01-15T10:00:00Z",
  "updatedAt": "2025-01-15T10:00:00Z"
}
```

Deleting a scope fails with `409 Conflict` if any config entries, variables, or clients reference its values.

---

## Groups

Groups organize projects, templates, and variables. They also serve as an access control boundary.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/groups` | List groups. |
| `POST` | `/api/groups` | Create a group. |
| `GET` | `/api/groups/{id}` | Get a group. |
| `PUT` | `/api/groups/{id}` | Update a group. Requires `If-Match`. |
| `DELETE` | `/api/groups/{id}` | Delete a group. Requires `If-Match`. |
| `GET` | `/api/groups/{id}/members` | List group members and their roles. |
| `PUT` | `/api/groups/{id}/members/{userId}` | Set a user's role in this group. |
| `DELETE` | `/api/groups/{id}/members/{userId}` | Remove a user from this group. |

**Create a group:**

```bash
curl -X POST http://localhost:8080/api/groups \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "Platform Team",
    "description": "Platform engineering"
  }'
```

**Set a group member's role:**

```bash
curl -X PUT http://localhost:8080/api/groups/GROUP_ID/members/USER_ID \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{"roleId": "ROLE_ID"}'
```

Deleting a group fails with `409 Conflict` if it contains projects or templates.

---

## Projects

A project represents one application or service that consumes configuration.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/projects` | List projects. |
| `POST` | `/api/projects` | Create a project. |
| `GET` | `/api/projects/{id}` | Get a project. |
| `PUT` | `/api/projects/{id}` | Update a project. Requires `If-Match`. |
| `DELETE` | `/api/projects/{id}` | Delete a project. Requires `If-Match`. |
| `PUT` | `/api/projects/{id}/templates/{templateId}` | Attach a template to the project. |
| `DELETE` | `/api/projects/{id}/templates/{templateId}` | Detach a template from the project. |

**Create a project:**

```bash
curl -X POST http://localhost:8080/api/projects \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "Backend API",
    "description": "Core REST API service",
    "groupId": "GROUP_ID",
    "templateIds": ["TEMPLATE_ID_1", "TEMPLATE_ID_2"]
  }'
```

Attaching a template that introduces duplicate config entry keys returns `409 Conflict`.

---

## Templates

Reusable sets of configuration entries shared across projects.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates` | List templates. |
| `POST` | `/api/templates` | Create a template. |
| `GET` | `/api/templates/{id}` | Get a template. |
| `PUT` | `/api/templates/{id}` | Update a template. Requires `If-Match`. |
| `DELETE` | `/api/templates/{id}` | Delete a template. Requires `If-Match`. |

**Create a template:**

```bash
curl -X POST http://localhost:8080/api/templates \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "Base Application Template",
    "description": "Core settings shared by all apps",
    "groupId": "GROUP_ID"
  }'
```

Omit `groupId` to create a global template. Deleting a template fails with `409 Conflict` if any project references it.

---

## Variables

Named placeholders with scope-aware values. Reference them in config entries using `{{variableName}}` syntax.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/variables` | List variables. Filter by `groupId`, `projectId`. |
| `POST` | `/api/variables` | Create a variable. |
| `GET` | `/api/variables/{id}` | Get a variable. Sensitive values masked by default. |
| `PUT` | `/api/variables/{id}` | Update a variable. Requires `If-Match`. |
| `DELETE` | `/api/variables/{id}` | Delete a variable. Requires `If-Match`. |

**Create a variable:**

```bash
curl -X POST http://localhost:8080/api/variables \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "DatabaseConnectionString",
    "scope": "Global",
    "isSensitive": true,
    "description": "Primary database connection string",
    "groupId": "GROUP_ID",
    "values": [
      {"value": "Server=localhost;Database=myapp;"},
      {"scopes": {"Environment": "prod"}, "value": "Server=prod-db.internal;Database=myapp;Encrypt=True;"}
    ]
  }'
```

Append `?decrypt=true` to GET requests to reveal sensitive values (requires `sensitive_values:decrypt` permission).

---

## Configuration Entries

Individual key-value pairs that make up your configuration.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/config-entries` | List entries. Filter by `ownerId`, `ownerType`. |
| `POST` | `/api/config-entries` | Create an entry. |
| `GET` | `/api/config-entries/{id}` | Get an entry. Sensitive values masked by default. |
| `PUT` | `/api/config-entries/{id}` | Update an entry. Requires `If-Match`. |
| `DELETE` | `/api/config-entries/{id}` | Delete an entry. Requires `If-Match`. |

**Create a config entry:**

```bash
curl -X POST http://localhost:8080/api/config-entries \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "key": "App:LogLevel",
    "valueType": "String",
    "ownerId": "PROJECT_OR_TEMPLATE_ID",
    "ownerType": "Project",
    "description": "Application log level",
    "isSensitive": false,
    "values": [
      {"value": "Information"},
      {"scopes": {"Environment": "dev"}, "value": "Debug"},
      {"scopes": {"Environment": "prod"}, "value": "Warning"}
    ]
  }'
```

Notes:

- `ownerType` is either `Project` or `Template`
- The first value with no `scopes` is the default
- Use `{{variableName}}` in values to reference variables
- Sensitive values are masked as `"***"` -- add `?decrypt=true` to see them

---

## Snapshots

Immutable, point-in-time captures of a project's fully resolved configuration.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/projects/{projectId}/snapshots` | List snapshots for a project. |
| `POST` | `/api/projects/{projectId}/snapshots` | Publish a new snapshot. |
| `GET` | `/api/projects/{projectId}/snapshots/{id}` | Get a snapshot. |
| `POST` | `/api/projects/{projectId}/snapshots/{id}/activate` | Activate (roll back to) a previous snapshot. |

**Publish a snapshot:**

```bash
curl -X POST http://localhost:8080/api/projects/PROJECT_ID/snapshots \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{"description": "Release 2.1 configuration"}'
```

Response:

```json
{
  "id": "019abc34-...",
  "projectId": "...",
  "snapshotVersion": 3,
  "description": "Release 2.1 configuration",
  "entryCount": 12,
  "publishedAt": "2025-01-15T12:00:00Z"
}
```

Publishing fails with `422 Unprocessable Content` if there are unresolved variable references.

**Roll back:**

```bash
curl -X POST http://localhost:8080/api/projects/PROJECT_ID/snapshots/SNAPSHOT_ID/activate \
  -H "api-version: 1.0"
```

---

## Clients

API keys that applications use to authenticate and receive configuration.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/projects/{projectId}/clients` | List clients for a project. |
| `POST` | `/api/projects/{projectId}/clients` | Create a client. Returns the secret (once). |
| `GET` | `/api/projects/{projectId}/clients/{id}` | Get a client. |
| `PUT` | `/api/projects/{projectId}/clients/{id}` | Update a client. Requires `If-Match`. |
| `DELETE` | `/api/projects/{projectId}/clients/{id}` | Delete a client. Requires `If-Match`. |

**Create a client:**

```bash
curl -X POST http://localhost:8080/api/projects/PROJECT_ID/clients \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "backend-api-prod",
    "scopes": {"Environment": "prod", "Region": "us-east"}
  }'
```

Response:

```json
{
  "id": "019abc56-...",
  "clientId": "019abc56-...",
  "clientSecret": "a1b2c3d4e5f6...",
  "name": "backend-api-prod",
  "scopes": {"Environment": "prod", "Region": "us-east"},
  "isActive": true
}
```

> **Warning:** The `clientSecret` is only returned at creation. Store it securely -- it cannot be retrieved again.

---

## Roles

Named permission bundles assigned to users.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/roles` | List roles. |
| `POST` | `/api/roles` | Create a role. |
| `GET` | `/api/roles/{id}` | Get a role. |
| `PUT` | `/api/roles/{id}` | Update a role. Requires `If-Match`. |
| `DELETE` | `/api/roles/{id}` | Delete a role. Requires `If-Match`. |

**Available permissions:**

`scopes:read`, `scopes:write`, `groups:read`, `groups:write`, `projects:read`, `projects:write`, `templates:read`, `templates:write`, `variables:read`, `variables:write`, `config-entries:read`, `config-entries:write`, `snapshots:read`, `snapshots:publish`, `clients:read`, `clients:write`, `roles:read`, `roles:write`, `users:read`, `users:write`, `sensitive_values:decrypt`, `audit:read`

**Default roles:** Viewer, Editor, Publisher, Admin (created at server startup).

---

## Users

User accounts for the management API.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/users` | List users. |
| `POST` | `/api/users` | Create a user. |
| `GET` | `/api/users/{id}` | Get a user. |
| `PUT` | `/api/users/{id}` | Update a user. Requires `If-Match`. |
| `DELETE` | `/api/users/{id}` | Delete a user. Requires `If-Match`. |
| `PUT` | `/api/users/{id}/password` | Change password (own account only, BuiltIn auth). |

---

## Audit Records

Immutable log of all changes.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/audit-records` | List audit records. Filter by `entityType`, `entityId`, `performedBy`. |
| `GET` | `/api/audit-records/{id}` | Get an audit record. |

Audit records are read-only. Each record includes the entity type, entity ID, action performed, who performed it, when, and field-level changes. Users can only see records for resources they have access to.

---

## Personal Access Tokens

Long-lived tokens for programmatic access (BuiltIn and External auth modes).

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/personal-access-tokens` | Create a PAT. Returns the token value (once). |
| `GET` | `/api/personal-access-tokens` | List your PATs. |
| `GET` | `/api/personal-access-tokens/{id}` | Get a PAT. |
| `DELETE` | `/api/personal-access-tokens/{id}` | Revoke a PAT. |

PATs are scoped to the current user. The full token value is only shown at creation.

---

## Client API

Endpoints used by applications (via the SDK or directly) to fetch configuration.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/client/config` | `ApiKey` | Fetch resolved configuration for the authenticated client. |
| `GET` | `/client/config/stream` | `ApiKey` | SSE stream for real-time configuration updates. |
| `GET` | `/client/health` | None | Health check. |

**Fetch config:**

```bash
curl http://localhost:8080/client/config \
  -H "Authorization: ApiKey CLIENT_ID:CLIENT_SECRET" \
  -H "api-version: 1.0"
```

Response:

```json
{
  "data": {
    "App:LogLevel": "Debug",
    "App:Name": "Backend API",
    "Database:Host": "localhost",
    "Database:Port": "5432"
  },
  "snapshotId": "019abc34-...",
  "snapshotVersion": 3
}
```

All values are strings. The SDK and your application handle type conversion.

The GET endpoint supports conditional requests -- pass `If-None-Match: "ETAG"` to receive `304 Not Modified` when config hasn't changed.

**Providing extra scopes at request time.** Clients (either `/client/config` or `/client/config/stream`) may send a `GroundControl-Scopes` header with additional scope dimensions beyond those bound to the Client entity. The value is a comma-separated list of URL-encoded `dimension:value` pairs, for example:

```
GroundControl-Scopes: Environment:prod,Region:eu-west
```

The server merges these with the Client entity's server-defined scopes. On key conflict, the server-defined scopes take priority. The `GroundControl.Link` SDK emits this header automatically when the `Scopes` option is populated.

---

## Health

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/healthz/liveness` | Process running (always 200). |
| `GET` | `/healthz/ready` | Dependencies healthy (MongoDB, change notifier). Returns 503 if unhealthy. |

Use for container orchestration probes. See [Deployment](../server/deployment.md) for details.
