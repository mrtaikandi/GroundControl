# Domain Model

This document defines the core domain entities, their relationships, ownership rules, and business rules that govern the GroundControl configuration management platform.

## Entity Overview

| Entity | Description |
|--------|-------------|
| **Scope** | A predefined dimension name with allowed values for validating configuration scopes |
| **Group** | Optional organizational unit that owns projects, templates, and variables |
| **Project** | A configuration container mapped 1:1 to a client application |
| **Template** | A reusable, scoped set of configuration entries shared across projects |
| **Variable** | A named placeholder with scope-aware values, used for interpolation in config values |
| **Configuration Entry** | An individual key-value pair with type, scope variants, and sensitivity flag |
| **Snapshot** | An immutable, pre-computed artifact containing all resolved config for a project |
| **Client** | An authentication credential tied to a specific project and scope combination |
| **User** | An admin identity with role-based access to manage configuration |
| **Personal Access Token** | A long-lived authentication token tied to a user, for programmatic access (CI/CD, CLI) |
| **Audit Record** | An immutable log entry tracking every change made to any entity |

---

## Entity Definitions

### Scope

A scope declares a named dimension and its allowed values. Scopes are system-level resources managed by System Admins.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `dimension` | string | The dimension name (e.g., `environment`, `region`, `tier`) |
| `allowedValues` | string[] | The set of valid values for this dimension (e.g., `["Production", "Staging", "Development"]`) |
| `description` | string? | Optional human-readable description |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- Dimension names must be unique across the system (case-insensitive).
- Allowed values are case-sensitive (exact match). For example, `"Production"` and `"production"` are distinct values.
- A scope used in any configuration entry, template, variable, or client must reference a valid dimension and value from a scope.
- Removing an allowed value is only permitted if no entity references that dimension-value pair.

---

### Group

An optional organizational unit that provides resource isolation. When groups are not used, all resources are globally accessible based on user roles.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `name` | string | Display name for the group |
| `description` | string? | Optional description |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- Group names must be unique (case-insensitive).
- A group can be deleted only if it has no owned projects, templates, or variables.
- Groups are optional. When no groups exist, users with system-wide grants have access to all resources (grants are still required — no grants means no access).

---

### Project

A configuration container that maps 1:1 to a client application or microservice.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `name` | string | Project name |
| `description` | string? | Optional description |
| `groupId` | Guid? | Owning group (null if groups are not used) |
| `templateIds` | Guid[] | Ordered list of template IDs applied to this project |
| `activeSnapshotId` | Guid? | Currently active snapshot (null if never published) |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- Project names must be unique within their group (or globally if no groups are used).
- A project can reference multiple templates, but the combined keys across all templates must not overlap. This is validated when adding/removing templates.
- When a project is deleted, all associated config entries, snapshots, clients, and project-level variables are also deleted.

---

### Template

A reusable collection of scoped configuration entries that can be shared across multiple projects.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `name` | string | Template name |
| `description` | string? | Optional description |
| `groupId` | Guid? | Owning group (null = global, accessible to all groups) |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Ownership:**
- `groupId = null` → Global template, managed by System Admins, available to all projects.
- `groupId = <id>` → Group-owned template, managed by group editors, available only to projects in the same group.

**Business Rules:**
- Template names must be unique within their scope (within a group, or globally for global templates).
- A template contains configuration entries (see [Configuration Entry](#configuration-entry)).
- When a template is referenced by projects, modifying its entries does not affect already-published snapshots (snapshots are immutable).
- Deleting a template is only permitted if no project currently references it.

---

### Variable

A named placeholder with scope-aware values that can be interpolated into configuration entry values using the `{{variableName}}` syntax.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `name` | string | Variable name (used in `{{name}}` interpolation) |
| `description` | string? | Optional description |
| `scope` | `global` or `project` | Whether this is a global or project-level variable |
| `groupId` | Guid? | Owning group for global variables (null = system-wide) |
| `projectId` | Guid? | Owning project (only for project-scope variables) |
| `values` | ScopedValue[] | List of scope-specific values (see below) |
| `isSensitive` | bool | Whether the variable value should be treated as sensitive |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**ScopedValue:**

| Field | Type | Description |
|-------|------|-------------|
| `scopes` | Dictionary<string, string> | Scope dimension-value pairs this value applies to (empty = default/unscoped) |
| `value` | string | The variable value for this scope combination |

**Two-Tier Resolution:**
1. When resolving a variable for a project + scope combination, first check for a project-level variable override.
2. If no project-level override exists (or no matching scope), fall back to the global variable value.
3. Within each tier, most-specific scope match wins (see [Scope Resolution](#scope-resolution)).

**Business Rules:**
- Variable names must be unique within their tier and ownership context (globally unique for global variables within a group, project-unique for project variables).
- A project-level variable with the same name as a global variable acts as an override for that project.
- The `{{...}}` interpolation syntax is only valid in **configuration entry** values, not in variable values. Variables must contain literal values only. A variable value containing `{{...}}` is rejected at write time.
- Deleting a global variable is only permitted if no template or configuration entry references it.

---

### Configuration Entry

An individual configuration key-value pair with type metadata, scope variants, and a sensitivity flag. Configuration entries belong to either a template or a project.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `key` | string | Configuration key using colon-separated hierarchy (e.g., `Logging:LogLevel:Default`) |
| `ownerId` | Guid | ID of the owning template or project |
| `ownerType` | `template` or `project` | Whether this entry belongs to a template or a project |
| `valueType` | string | .NET type name: `String`, `Int32`, `Int64`, `Double`, `Decimal`, `Boolean`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` |
| `values` | ScopedValue[] | List of scope-specific values |
| `isSensitive` | bool | Whether this value should be encrypted at rest and masked in logs/UIs |
| `description` | string? | Optional description of what this key configures |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- Keys must be unique within their owner (no duplicate keys within the same template or project).
- The `value` field is always stored as a string representation. The `valueType` determines how the value is validated on write and deserialized by the client SDK.
- Values can contain `{{variableName}}` references that are interpolated at snapshot creation time. Config entries **can** be saved with references to variables that don't yet exist — variable resolution is validated at publish time, not write time.
- Template and project entries for the same key may coexist when a template is attached. During snapshot resolution, the project-level entry overrides the template entry for that key.

---

### Snapshot

An immutable, point-in-time copy of all resolved configuration for a project. Created explicitly by an admin "publish" action.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `projectId` | Guid | The project this snapshot belongs to |
| `snapshotVersion` | long | Monotonically increasing version number within the project |
| `entries` | ResolvedEntry[] | The fully resolved configuration entries |
| `publishedAt` | DateTimeOffset | When the snapshot was published |
| `publishedBy` | Guid | User ID of the publisher |
| `description` | string? | Optional publish note/description |

**ResolvedEntry:**

| Field | Type | Description |
|-------|------|-------------|
| `key` | string | Configuration key |
| `valueType` | string | .NET type name |
| `values` | ScopedValue[] | Scope-specific resolved values |
| `isSensitive` | bool | Sensitivity flag |

**Business Rules:**
- Snapshots are immutable. Once created, they cannot be modified.
- Each project has at most one active snapshot (`activeSnapshotId` on the project).
- Publishing creates a new snapshot and sets it as active. The previous snapshot remains stored for rollback.
- Rollback re-activates a previous snapshot by changing the project's `activeSnapshotId`.
- Snapshot creation process:
  1. Collect all config entries from attached templates and the project itself.
  2. Project-level entries override template entries with the same key.
  3. For each scope variant of each entry, resolve variable references by finding the best-matching variable value for that scope.
  4. Encrypt sensitive values.
  5. Store the resulting immutable snapshot.
- Old snapshots should be retained according to a configurable retention policy (e.g., keep last N snapshots per project).
- Snapshot documents can be large. The MongoDB 16MB document size limit applies. For v1, snapshot overflow (chunking into a separate collection) is not supported. If the generated snapshot exceeds the document size limit, publish fails with an error. Operational guidance: keep entries and scope variants within reasonable limits per project.

---

### Client

An authentication credential that grants a client application access to configuration for a specific project and scope combination.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `projectId` | Guid | The project this key grants access to |
| `scopes` | Dictionary<string, string> | The scope combination this key is valid for (e.g., `{ "environment": "Production", "region": "EU" }`) |
| `secret` | string | Client secret, protected at rest via `IValueProtector` |
| `name` | string | Human-readable name for the key |
| `isActive` | bool | Whether the key is currently active |
| `expiresAt` | DateTimeOffset? | Optional expiration date |
| `lastUsedAt` | DateTimeOffset? | Last time the key was used |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- The `clientId` (document ID) and raw `clientSecret` are returned at creation time. The secret is encrypted at rest.
- A client is uniquely identified by its `id`. Multiple clients may target the same project + scope combination to support key rotation.
- Deactivated or expired keys are rejected at authentication.
- When a client connects with an API key, the server uses the key's `projectId` and `scopes` to resolve the appropriate config from the active snapshot.

---

### User

A user identity that represents resource ownership and access control. Authentication is handled by a pluggable layer; the user entity stores identity, grants, and external provider links.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `username` | string | Unique username |
| `email` | string | Email address |
| `grants` | Grant[] | Role assignments with resource scope and optional conditions |
| `isActive` | bool | Whether the user account is active |
| `externalId` | string? | External identity provider subject claim (`sub`). Null for built-in users. |
| `externalProvider` | string? | Identity provider name (e.g., `"entra"`, `"keycloak"`). Null for built-in users. |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Grant:**

| Field | Type | Description |
|-------|------|-------------|
| `resource` | Guid? | null = system-wide (global fallback), Guid = specific group |
| `roleId` | Guid | References a role in the `roles` collection |
| `conditions` | Dictionary<string, string[]>? | Optional scope value filter (e.g., `{ "environment": ["Testing"] }`). Null = unrestricted. |

**Business Rules:**
- Users with no grants (`grants = []`) have no access to any resources.
- A system-wide grant (`resource = null`) acts as a **global fallback** — its permissions apply to all groups.
- A user can have multiple grants targeting different groups with different roles.
- Effective permissions for a given group are the **union** of all applicable grants (system-wide + group-specific). Permissions are additive.
- Conditions restrict which configuration scope values (e.g., `environment=Production`) the user can see/edit. They do not affect resource-level access. If any applicable grant has null conditions, the user sees all scope values.
- When the authentication mode is `None`, all requests are treated as a well-known system user (`Guid.Empty`) with a system-wide Admin grant.
- `externalId` and `externalProvider` are used to link users to external identity providers (Entra ID, Keycloak, etc.) for JIT provisioning and migration.

See [Authentication & Authorization](Authentication-Authorization.md) for the full permission model, authorization matrix, and authentication modes.

---

### Role

A named collection of permissions stored in the database. Roles define what actions a user can perform. Four default roles are seeded at startup, but admins can create, modify, and delete any role.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `name` | string | Unique role name (e.g., "Viewer", "Editor", "Publisher", "Admin") |
| `description` | string? | Human-readable description |
| `permissions` | string[] | List of permission strings (e.g., `["projects:read", "projects:write"]`) |
| `version` | long | Optimistic concurrency version |
| `createdAt` | DateTimeOffset | Creation timestamp |
| `createdBy` | Guid | User ID of creator |
| `updatedAt` | DateTimeOffset | Last modification timestamp |
| `updatedBy` | Guid | User ID of last modifier |

**Business Rules:**
- Permissions are constants defined in code (`resource:action` format). Roles reference them by string value. See [Authentication & Authorization](Authentication-Authorization.md#permissions) for the full permission list.
- Role names must be unique.
- A role cannot be deleted while referenced by any user's grants. Affected users must be reassigned first.
- Four default roles (Viewer, Editor, Publisher, Admin) are seeded at startup. They can be freely modified or deleted by admins.
- Custom roles can contain any combination of the 22 defined permissions.

---

### Personal Access Token

A long-lived authentication token tied to a user's identity, used for programmatic access (CI/CD pipelines, CLI tools, automation scripts). Available in Built-In and External authentication modes only.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `userId` | Guid | The user this token belongs to |
| `name` | string | Human-readable label (e.g., "CI pipeline token") |
| `tokenPrefix` | string | First 8 characters of the raw token (for display purposes) |
| `tokenHash` | string | SHA-256 hash of the full token |
| `expiresAt` | DateTimeOffset? | Optional expiration date |
| `lastUsedAt` | DateTimeOffset? | Last time the token was used for authentication |
| `isRevoked` | bool | Whether the token has been revoked |
| `permissions` | string[]? | Optional permission whitelist. Null = full user permissions. |
| `createdAt` | DateTimeOffset | Creation timestamp |

**Business Rules:**
- Tokens are prefixed with `gc_pat_` followed by a cryptographically random value (minimum 256 bits of entropy).
- The raw token value is returned only at creation time and cannot be retrieved afterward (same pattern as client secrets).
- Only the SHA-256 hash is stored. Token validation computes the hash and looks up by hash.
- PATs support optional permission scoping via the `permissions` field. When `null` (default), the PAT inherits all permissions of the owning user. When set, effective permissions are the intersection of the user's grant-derived permissions and the token's allowed permissions. The `permissions` field is a whitelist — it can only restrict, never expand, the owner's access. Validation at creation ensures all listed values are valid permission constants.
- A user can have a configurable maximum number of active PATs (default: 10).
- Tokens can have an optional expiration (default: 90 days, configurable maximum: 365 days).
- Revoking a token is immediate and permanent.
- If the owning user's `isActive` flag is set to `false`, all their PATs are effectively disabled.

---

### Audit Record

An immutable log entry that captures every modification to any entity.

| Field | Type | Description |
|-------|------|-------------|
| `id` | Guid (UUIDv7) | Unique identifier, generated via `Guid.CreateVersion7()` |
| `entityType` | string | Type of entity changed (e.g., `Project`, `Template`, `ConfigEntry`) |
| `entityId` | Guid | ID of the changed entity |
| `groupId` | Guid? | Owning group of the changed entity (null for system-level entities). Denormalized for efficient scoped queries. |
| `action` | string | The action performed (`Created`, `Updated`, `Deleted`, `Published`, `Activated`) |
| `performedBy` | Guid | User ID of the actor |
| `performedAt` | DateTimeOffset | When the action occurred |
| `changes` | FieldChange[]? | List of field-level changes (for updates) |
| `metadata` | Dictionary<string, string>? | Additional context (e.g., snapshot version for publish actions) |

**FieldChange:**

| Field | Type | Description |
|-------|------|-------------|
| `field` | string | Name of the changed field |
| `oldValue` | string? | Previous value (serialized, masked if sensitive) |
| `newValue` | string? | New value (serialized, masked if sensitive) |

**Business Rules:**
- Audit records are immutable and append-only. They cannot be modified or deleted through the API.
- Sensitive values are masked in audit records (e.g., `"***"` or partial masking).
- Audit records are stored via a pluggable audit store interface. The default implementation uses MongoDB.
- Audit record visibility is scoped by the user's grants. Users with system-wide `audit:read` see all records; other users see records for their granted groups plus system-level records. See [Authentication & Authorization](Authentication-Authorization.md#scoped-audit-records).

---

## Scope Resolution

When delivering configuration to a client, the server must resolve which scoped value to use for each key based on the client's registered scope combination.

### Algorithm

Given a client with scopes `{ environment: "Production", region: "EU" }` and a config entry with the following scoped values:

| Scoped Value | Scopes | Value |
|--------------|--------|-------|
| A | `{}` (unscoped/default) | `"default-value"` |
| B | `{ environment: "Production" }` | `"prod-value"` |
| C | `{ region: "EU" }` | `"eu-value"` |
| D | `{ environment: "Production", region: "EU" }` | `"prod-eu-value"` |

**Resolution steps:**

1. **Filter**: Keep only scoped values where every dimension-value pair in the scope is present in the client's scopes. Values A, B, C, and D all match.
2. **Rank by specificity**: Count the number of matching scope dimensions. D has 2, B and C have 1, A has 0.
3. **Select most specific**: D wins with the highest specificity (2 dimensions).

**Tie-breaking:** If two scoped values have the same number of matching dimensions and both match the client's scopes, this is an ambiguous configuration. The system should prevent this at write time by validating that no two scoped values on the same key have the same specificity for any possible client scope combination that could match both.

### Fallback Chain

For each key, if no scoped value matches the client's scopes, the unscoped (default) value is used. If no default value exists, the key is omitted from the client's configuration.

---

## Snapshot Resolution Process

When an admin publishes a snapshot for a project:

1. **Collect entries**: Gather all config entries from the project's attached templates and the project's own entries.
2. **Merge with override**: For any key that exists in both a template and the project, the project-level entry takes precedence (full replacement of all scoped values for that key).
3. **Interpolate variables**: For each scoped value in each entry:
   a. Find all `{{variableName}}` references in the value string.
   b. For each variable, resolve its value using the two-tier system:
      - First check for a project-level variable with a matching scope.
      - Fall back to the global variable with a matching scope.
      - Use the same scope resolution algorithm (most-specific match wins).
   c. Replace the placeholder with the resolved variable value.
4. **Validate**: Ensure all variable references were resolved (no unresolved `{{...}}` placeholders remain).
5. **Encrypt sensitive values**: Encrypt values marked as sensitive using the configured encryption provider.
6. **Store snapshot**: Persist the immutable snapshot with a new incremented version number.
7. **Activate**: Set the project's `activeSnapshotId` to the new snapshot.
8. **Notify**: Trigger the change notification system to alert connected clients.

**Failure modes and atomicity:**

- Steps 1–5 are pure computation with no side effects. If any step fails, no snapshot is created.
- Step 6 (Store): `snapshotVersion` monotonicity is guaranteed by the `{ ProjectId, SnapshotVersion }` unique index. Concurrent publishes to the same project will cause one to fail with a duplicate key error; the caller retries with an incremented version.
- Step 7 (Activate): Uses optimistic concurrency on the project's `version` field. If a concurrent publish already activated a different snapshot, this step fails with a 409 Conflict.
- Step 8 (Notify): If notification fails, the change is self-healing — the `MongoChangeStreamNotifier` watches `activeSnapshotId` changes on the `projects` collection and will propagate the update independently.
- If step 6 succeeds but step 7 fails, an orphaned snapshot exists in MongoDB. This is harmless — it is never activated and will be cleaned up by the snapshot retention policy.

---

## Entity Relationship Summary

```
System Admin manages ──→ Scopes (system-wide)
                    ──→ Global Templates (groupId = null)
                    ──→ Global Variables (groupId = null)
                    ──→ Users
                    ──→ Groups

Group owns ──→ Projects
           ──→ Templates (group-scoped)
           ──→ Variables (group-scoped, global tier)

Project owns ──→ Configuration Entries (project-level)
             ──→ Variables (project-level overrides)
             ──→ Snapshots
             ──→ Clients

Project references ──→ Templates (many, no key overlap)

Template owns ──→ Configuration Entries (template-level)

Snapshot contains ──→ Resolved Entries (denormalized, immutable)

Client grants access to ──→ One Project + One Scope Combination

User has ──→ Grants (role + resource scope + optional conditions)
User owns ──→ Personal Access Tokens (for programmatic API access)

Grant references ──→ Role (named permission bundle)
```
