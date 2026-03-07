# Data Model

This document defines the MongoDB schema design for GroundControl, including collection schemas, index strategies, and storage patterns.

See [Domain Model](Domain-Model.md) for entity definitions and business rules.

---

## Database Overview

GroundControl uses a single MongoDB database with the following collections. Collection names are **all lowercase, plural, and use snake_case** for multi-word names.

| Collection | Description |
|------------|-------------|
| `scopes` | Predefined scope dimensions and their allowed values |
| `groups` | Organizational units for resource ownership |
| `projects` | Configuration containers mapped to client applications |
| `templates` | Reusable configuration entry sets |
| `variables` | Named placeholders with scope-aware values |
| `config_entries` | Individual configuration key-value pairs |
| `snapshots` | Immutable, pre-computed configuration artifacts |
| `clients` | Client credentials (project + scope authentication) |
| `users` | Admin user accounts |
| `roles` | Named permission bundles (custom and default) |
| `identity_users` | ASP.NET Identity credentials (Built-In auth mode only) |
| `personal_access_tokens` | Long-lived API tokens for programmatic access |
| `refresh_tokens` | JWT refresh tokens with rotation (Built-In auth mode only) |
| `audit_records` | Immutable change log |

---

## ID Strategy

All entity IDs use **UUIDv7** (`Guid.CreateVersion7()` in .NET). UUIDv7 embeds a Unix timestamp in the most significant bits, providing:

- **Natural chronological ordering** — IDs are sortable by creation time without a separate timestamp field.
- **No central coordination** — IDs can be generated on any server instance without a sequence or counter.
- **MongoDB compatibility** — Stored as MongoDB's native `BinData` subtype 4 (UUID) using the `GuidRepresentation.Standard` setting, which preserves sort order.
- **Index efficiency** — Sequential UUIDs avoid the random-write fragmentation of UUIDv4, resulting in better B-tree index performance.

---

## Collection Schemas

> **Collation convention:** All unique indexes on name/dimension fields use MongoDB case-insensitive collation (`{ locale: "en", strength: 2 }`) to enforce case-insensitive uniqueness without shadow fields.

### `scopes`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Dimension` | `string` | Dimension name (e.g., `environment`, `region`) |
| `AllowedValues` | `List<string>` | Valid values for this dimension |
| `Description` | `string?` | Optional human-readable description |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ Dimension: 1 }` | Unique, case-insensitive collation | Enforce unique dimension names, lookup by dimension |

---

### `groups`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Name` | `string` | Display name for the group |
| `Description` | `string?` | Optional description |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ Name: 1 }` | Unique, case-insensitive collation | Enforce unique group names, lookup by name |

---

### `projects`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Name` | `string` | Project name |
| `Description` | `string?` | Optional description |
| `GroupId` | `Guid?` | Owning group (null if groups are not used) |
| `TemplateIds` | `List<Guid>` | Ordered list of template IDs applied to this project |
| `ActiveSnapshotId` | `Guid?` | Currently active snapshot (null if never published) |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ GroupId: 1, Name: 1 }` | Unique, case-insensitive collation | Enforce unique project names within a group |
| `{ GroupId: 1 }` | Standard | List projects by group |
| `{ ActiveSnapshotId: 1 }` | Standard | Find project by active snapshot |

---

### `templates`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Name` | `string` | Template name |
| `Description` | `string?` | Optional description |
| `GroupId` | `Guid?` | Owning group (null = global, accessible to all groups) |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ GroupId: 1, Name: 1 }` | Unique, case-insensitive collation | Enforce unique template names within scope |
| `{ GroupId: 1 }` | Standard | List templates by group (null = global) |

---

### `variables`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Name` | `string` | Variable name (used in `{{name}}` interpolation) |
| `Description` | `string?` | Optional description |
| `Scope` | `VariableScope` | Enum: `Global`, `Project` |
| `GroupId` | `Guid?` | Owning group for global variables (null = system-wide) |
| `ProjectId` | `Guid?` | Owning project (only for project-scope variables) |
| `Values` | `List<ScopedValue>` | Scope-specific values (see below) |
| `IsSensitive` | `bool` | Whether the variable value should be treated as sensitive |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Embedded: `ScopedValue`**

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Scopes` | `Dictionary<string, string>` | Scope dimension-value pairs (empty = default/unscoped) |
| `Value` | `string` | The variable value for this scope combination |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ Scope: 1, GroupId: 1, Name: 1 }` | Unique, case-insensitive collation (partial: where `Scope = "Global"`) | Enforce unique global variable names within a group |
| `{ Scope: 1, ProjectId: 1, Name: 1 }` | Unique, case-insensitive collation (partial: where `Scope = "Project"`) | Enforce unique project variable names |
| `{ ProjectId: 1 }` | Standard | List variables for a project |
| `{ GroupId: 1 }` | Standard | List global variables for a group |

---

### `config_entries`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Key` | `string` | Configuration key using colon hierarchy (e.g., `Logging:LogLevel:Default`) |
| `OwnerId` | `Guid` | ID of the owning template or project |
| `OwnerType` | `ConfigEntryOwnerType` | Enum: `Template`, `Project` |
| `ValueType` | `string` | .NET type name (see supported types below) |
| `Values` | `List<ScopedValue>` | Scope-specific values |
| `IsSensitive` | `bool` | Whether this value should be encrypted at rest and masked in logs |
| `Description` | `string?` | Optional description of what this key configures |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Supported `ValueType` values:** `String`, `Int32`, `Int64`, `Double`, `Decimal`, `Boolean`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ OwnerId: 1, OwnerType: 1, Key: 1 }` | Unique | Enforce unique keys within an owner |
| `{ OwnerId: 1, OwnerType: 1 }` | Standard | List all entries for a template or project |
| `{ Key: 1 }` | Standard | Search/filter entries by key pattern |

**Notes:**
- `Value` within scoped values is always a string serialization. Validation against `ValueType` occurs on write.
- Values may contain `{{variableName}}` interpolation placeholders.
- Sensitive values are stored encrypted at rest (see [Security Model](Security-Model.md)).

---

### `snapshots`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `ProjectId` | `Guid` | The project this snapshot belongs to |
| `SnapshotVersion` | `long` | Monotonically increasing version number within the project |
| `Entries` | `List<ResolvedEntry>` | Fully resolved configuration entries (see below) |
| `PublishedAt` | `DateTimeOffset` | When the snapshot was published |
| `PublishedBy` | `Guid` | User ID of the publisher |
| `Description` | `string?` | Optional publish note/description |

**Embedded: `ResolvedEntry`**

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Key` | `string` | Configuration key |
| `ValueType` | `string` | .NET type name |
| `IsSensitive` | `bool` | Sensitivity flag |
| `Values` | `List<ScopedValue>` | Scope-specific resolved values |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ ProjectId: 1, SnapshotVersion: -1 }` | Unique | Enforce unique versions per project, efficient lookup of latest/specific versions |
| `{ ProjectId: 1, PublishedAt: -1 }` | Standard | List snapshots by project in chronological order |

**Notes:**
- Snapshot documents are immutable. Once written, they are never updated.
- The `Entries` list is a denormalized, fully resolved copy of all configuration for the project at publish time.
- Variable interpolation has already been applied. All `{{...}}` placeholders are resolved.
- Sensitive values are stored encrypted within the snapshot.
- Snapshot documents can be large. The MongoDB 16MB document size limit applies. For v1, snapshot overflow (separate collection for entries referenced by snapshot ID) is not supported. Publish fails when the generated snapshot exceeds the document size limit. Operational guidance: keep entries and scope variants within reasonable limits per project.

---

### `clients`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7). Also serves as the `clientId` for authentication. |
| `ProjectId` | `Guid` | The project this key grants access to |
| `Scopes` | `Dictionary<string, string>` | Scope combination this key is valid for |
| `Secret` | `string` | Client secret protected at rest via `IValueProtector` |
| `Name` | `string` | Human-readable name for the key |
| `IsActive` | `bool` | Whether the key is currently active |
| `ExpiresAt` | `DateTimeOffset?` | Optional expiration date |
| `LastUsedAt` | `DateTimeOffset?` | Last time the key was used |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ ProjectId: 1 }` | Standard | List keys for a project |

**Notes:**
- Authentication uses a `clientId` + `clientSecret` pair. The `clientId` is the document's `Id` (used for O(1) lookup), and the `clientSecret` is verified by decrypting `Secret` and comparing.
- The raw `clientSecret` is generated and returned only at creation time.

---

### `users`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Username` | `string` | Unique username |
| `Email` | `string` | Email address |
| `Grants` | `List<Grant>` | Role assignments with resource scope and optional conditions (see below) |
| `IsActive` | `bool` | Whether the user account is active |
| `ExternalId` | `string?` | External identity provider subject claim (`sub`). Null for built-in users. |
| `ExternalProvider` | `string?` | Identity provider name (e.g., `"entra"`, `"keycloak"`). Null for built-in users. |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Embedded: `Grant`**

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Resource` | `Guid?` | null = system-wide (global fallback), Guid = specific groupId |
| `RoleId` | `Guid` | References `roles` collection |
| `Conditions` | `Dictionary<string, List<string>>?` | Scope value filters (e.g., `{ "environment": ["Testing"] }`). Null = unrestricted. |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ Username: 1 }` | Unique, case-insensitive collation | Enforce unique usernames, lookup by username |
| `{ Email: 1 }` | Unique, case-insensitive collation | Enforce unique emails, lookup by email |
| `{ "Grants.Resource": 1 }` | Standard | Find users by group |
| `{ ExternalProvider: 1, ExternalId: 1 }` | Unique, sparse (where `ExternalId != null`) | JIT provisioning lookup by external identity |

---

### `roles`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `Name` | `string` | Unique role name (e.g., "Viewer", "Editor", "Publisher", "Admin") |
| `Description` | `string?` | Human-readable description |
| `Permissions` | `List<string>` | Permission strings (e.g., `["projects:read", "projects:write"]`) |
| `Version` | `long` | Optimistic concurrency version |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `CreatedBy` | `Guid` | User ID of creator |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp |
| `UpdatedBy` | `Guid` | User ID of last modifier |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ Name: 1 }` | Unique, case-insensitive collation | Enforce unique role names, lookup by name |

**Notes:**
- Four default roles (Viewer, Editor, Publisher, Admin) are seeded at startup. See [Authentication & Authorization](Authentication-Authorization.md#roles) for the default permission mappings.
- Admins can freely create, modify, and delete any role (including defaults).
- A role cannot be deleted while referenced by any user's grants.
- Permissions are constants defined in code (`resource:action` format, e.g., `projects:read`). Roles store them as string arrays.

---

### `identity_users` (Built-In Auth Mode Only)

Managed by ASP.NET Identity's MongoDB store. This collection only exists when the authentication mode is `BuiltIn`. The `Id` field matches the corresponding `users` document, linking domain identity to authentication credentials.

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key — same ID as the corresponding `users` document |
| `UserName` | `string` | Username (synced with `users.Username`) |
| `NormalizedUserName` | `string` | Uppercase username for case-insensitive lookups |
| `Email` | `string` | Email address |
| `NormalizedEmail` | `string` | Uppercase email for case-insensitive lookups |
| `EmailConfirmed` | `bool` | Always `true` (admin-created users) |
| `PasswordHash` | `string` | PBKDF2 password hash (managed by ASP.NET Identity) |
| `SecurityStamp` | `string` | Changed on password reset to invalidate existing sessions |
| `ConcurrencyStamp` | `string` | Optimistic concurrency |
| `LockoutEnd` | `DateTimeOffset?` | Lockout expiry timestamp |
| `LockoutEnabled` | `bool` | Whether account lockout is enabled |
| `AccessFailedCount` | `int` | Consecutive failed login attempts |

Indexes are created automatically by the ASP.NET Identity MongoDB provider.

**Notes:**
- This collection does not exist in `External` or `None` authentication modes.
- The `users` collection remains the authoritative source for domain fields (grants, active status).
- When switching from `BuiltIn` to `External` mode, the `identity_users` collection becomes dormant and can be cleaned up.

---

### `personal_access_tokens`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `UserId` | `Guid` | Owning user |
| `Name` | `string` | Human-readable label (e.g., "CI pipeline token") |
| `TokenPrefix` | `string` | First 8 characters of the raw token (for display: `gc_pat_a1b2...`) |
| `TokenHash` | `string` | SHA-256 hash of the full token |
| `ExpiresAt` | `DateTimeOffset?` | Optional expiration date |
| `LastUsedAt` | `DateTimeOffset?` | Last time the token was used for authentication |
| `IsRevoked` | `bool` | Whether the token has been revoked |
| `Permissions` | `string[]?` | Optional permission whitelist. Null = inherit all user permissions. |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ TokenHash: 1 }` | Unique | Lookup by hashed token during authentication |
| `{ UserId: 1 }` | Standard | List tokens for a user |

**Notes:**
- Only the SHA-256 hash is stored. The raw token is returned only at creation time.
- Token format: `gc_pat_<random>` with minimum 256 bits of entropy.
- The `gc_pat_` prefix enables the bearer token handler to instantly route PATs to database lookup vs. JWT signature validation.
- Available in `BuiltIn` and `External` auth modes only.

---

### `refresh_tokens` (Built-In Auth Mode Only)

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `UserId` | `Guid` | Owning user |
| `FamilyId` | `Guid` | Token family identifier. Generated on initial login; inherited by rotated tokens. |
| `TokenHash` | `string` | SHA-256 hash of the refresh token |
| `ExpiresAt` | `DateTimeOffset` | Expiration timestamp |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `RevokedAt` | `DateTimeOffset?` | When revoked (null if active) |
| `ReplacedByTokenId` | `Guid?` | Points to the replacement token in the rotation chain |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ TokenHash: 1 }` | Unique | Lookup by hashed token |
| `{ UserId: 1 }` | Standard | List/revoke all tokens for a user |
| `{ FamilyId: 1 }` | Standard | Revoke all tokens in a rotation family |
| `{ ExpiresAt: 1 }` | TTL | Auto-cleanup of expired tokens |

**Notes:**
- Each refresh token can only be used once (rotation). On use, it is revoked and a new token is issued.
- If a revoked token is reused (replay attack), all tokens with the same `FamilyId` are revoked (family revocation). This preserves other active sessions.
- Only exists in `BuiltIn` authentication mode.

---

### `audit_records`

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Id` | `Guid` | Primary key (UUIDv7) |
| `EntityType` | `string` | Type of entity changed (e.g., `Project`, `Template`, `ConfigEntry`) |
| `EntityId` | `Guid` | ID of the changed entity |
| `GroupId` | `Guid?` | Owning group of the changed entity. Null for system-level entities. Denormalized for scoped queries. |
| `Action` | `string` | Action performed (`Created`, `Updated`, `Deleted`, `Published`, `Activated`) |
| `PerformedBy` | `Guid` | User ID of the actor |
| `PerformedAt` | `DateTimeOffset` | When the action occurred |
| `Changes` | `List<FieldChange>?` | Field-level changes for updates (see below) |
| `Metadata` | `Dictionary<string, string>?` | Additional context (e.g., snapshot version for publish actions) |

**Embedded: `FieldChange`**

| Field | .NET Type | Description |
|-------|-----------|-------------|
| `Field` | `string` | Name of the changed field |
| `OldValue` | `string?` | Previous value (serialized, masked if sensitive) |
| `NewValue` | `string?` | New value (serialized, masked if sensitive) |

**Indexes:**
| Index | Type | Purpose |
|-------|------|---------|
| `{ EntityType: 1, EntityId: 1, PerformedAt: -1 }` | Standard | View history for a specific entity |
| `{ PerformedBy: 1, PerformedAt: -1 }` | Standard | View actions by a specific user |
| `{ PerformedAt: -1 }` | Standard | Global audit log (reverse chronological) |
| `{ EntityType: 1, PerformedAt: -1 }` | Standard | Filter audit log by entity type |
| `{ GroupId: 1, PerformedAt: -1 }` | Standard | Scoped audit log queries (filter by user's group access) |

**Notes:**
- This collection is append-only. No updates or deletes are permitted through the application.
- Sensitive field values are masked in `OldValue` and `NewValue`.
- A TTL index can be added if audit record retention needs to be limited (e.g., `{ PerformedAt: 1 }, { expireAfterSeconds: ... }`).
- The audit store is accessed through a pluggable interface. This MongoDB schema is the default implementation.
- The `GroupId` field enables scoped audit visibility. See [Authentication & Authorization](Authentication-Authorization.md#scoped-audit-records).

---

## Optimistic Concurrency

Entities that support concurrent editing (projects, templates, variables, config entries, scope definitions, groups, users, clients, roles) include a `version` field.

> **Note:** `snapshotVersion` on snapshots is a sequence number, not a concurrency field — snapshots are immutable and never updated.

**Write pattern:**
```
// Pseudocode
filter: { _id: id, version: expectedVersion }
update: { $set: { ...fields, version: expectedVersion + 1, updatedAt: now } }
```

If the update matches zero documents, the version has changed since the client last read it, and the operation is rejected with a `409 Conflict` response.

---

## Change Streams

For the multi-instance notification backplane, GroundControl watches changes on the `projects` collection, specifically the `activeSnapshotId` field. When this field changes, it signals that a new snapshot has been activated (either a new publish or a rollback).

**Watch target:** `projects` collection
**Change filter:** Operations where `activeSnapshotId` is modified
**Notification payload:** `{ projectId, newSnapshotId }`

This is used by the `MongoChangeStreamNotifier` implementation (see [Deployment Architecture](Deployment-Architecture.md)).

---

## Data Retention

| Data Type | Retention Policy |
|-----------|-----------------|
| Snapshots | Configurable: keep last N per project (default: 50). Cleanup runs after each publish — snapshots beyond the retention count are deleted oldest-first. The active snapshot is never deleted. Rollback to a deleted snapshot returns 404. |
| Audit records | Configurable: TTL-based or unlimited (default: unlimited) |
| Clients | Soft delete (deactivate). A background hosted service hard-deletes deactivated and expired clients after a configurable grace period (default: 30 days). Configured via `Clients:CleanupGracePeriodDays` and `Clients:CleanupInterval` (default: daily). |
| All other entities | Retained until explicitly deleted |
