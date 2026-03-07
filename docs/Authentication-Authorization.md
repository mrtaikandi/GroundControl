# Authentication & Authorization

This document defines the pluggable authentication and authorization system for GroundControl's Management API. Client API authentication (`clientId` + `clientSecret`) is covered in the [Security Model](Security-Model.md).

---

## Overview

GroundControl supports three authentication modes, selected per instance via configuration:

| Mode | Use Case | How It Works |
|------|----------|-------------|
| **None** | Development, homelab, basic use | All requests treated as admin. No login required. |
| **BuiltIn** | Small/medium teams, self-hosted with UI | ASP.NET Identity with MongoDB. Cookie + JWT auth. Admin-created users. |
| **External** | Enterprise, SSO | OIDC integration with Entra ID, Keycloak, OpenIddict, etc. |

Only one mode is active per instance. The Management API host binds the `GroundControl` section into `GroundControlOptions`, and the active auth mode is selected from `GroundControlOptions.Security.AuthenticationMode`:

```json
{
  "GroundControl": {
    "Security": {
      "AuthenticationMode": "None"
    }
  }
}
```

---

## Pluggable Interface: `IAuthConfigurator`

Authentication follows the same pluggable pattern as `IValueProtector`, `IChangeNotifier`, and `IAuditStore`.

```csharp
public interface IAuthConfigurator
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void ConfigureMiddleware(IApplicationBuilder app);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

**Root configuration model:**

```csharp
public sealed partial class GroundControlOptions
{
  public const string SectionName = "GroundControl";

  public SecurityOptions Security { get; init; } = new();

  [OptionsValidator]
  internal sealed partial class Validator : IValidateOptions<GroundControlOptions>;
}

public sealed partial class SecurityOptions
{
  public AuthenticationMode AuthenticationMode { get; init; } = AuthenticationMode.None;

  [OptionsValidator]
  internal sealed partial class Validator : IValidateOptions<SecurityOptions>;
}

public enum AuthenticationMode
{
  None,
  BuiltIn,
  External
}
```

`AddGroundControlOptions(...)` binds the `GroundControl` section once at startup, validates it with the generated validators, and registers the resulting singleton `GroundControlOptions` plus `IOptions<GroundControlOptions>`. This root options model is specific to the Management API host; it does not move non-security sections such as `Persistence:MongoDb`.

**Registration at startup:**

```csharp
var groundControlOptions = builder.Services.AddGroundControlOptions(builder.Configuration);

IAuthConfigurator configurator = groundControlOptions.Security.AuthenticationMode switch
{
  AuthenticationMode.BuiltIn => new BuiltInAuthConfigurator(groundControlOptions),
  AuthenticationMode.External => new ExternalAuthConfigurator(groundControlOptions),
    _ => new NoAuthConfigurator()
};

configurator.ConfigureServices(builder.Services, builder.Configuration);
// ...
configurator.ConfigureMiddleware(app);
configurator.MapEndpoints(app);
```

---

## Permission Model

GroundControl uses a hybrid permission model: **fine-grained permissions** are the authorization primitives, and **roles** are named permission bundles stored in the database. Default roles are provided out of the box, but admins can create, modify, and delete any role.

### Permissions

22 permissions organized as `resource:action`. `write` encompasses create, update, and delete.

| Resource | Permissions | Description |
|----------|-------------|-------------|
| Scopes | `scopes:read`, `scopes:write` | View / manage scope definitions |
| Groups | `groups:read`, `groups:write` | View / manage groups (includes membership management) |
| Users | `users:read`, `users:write` | View / manage user accounts and grants |
| Roles | `roles:read`, `roles:write` | View / manage role definitions |
| Projects | `projects:read`, `projects:write` | View / manage projects |
| Templates | `templates:read`, `templates:write` | View / manage templates |
| Variables | `variables:read`, `variables:write` | View / manage variables |
| Config Entries | `config-entries:read`, `config-entries:write` | View / manage configuration entries |
| Snapshots | `snapshots:read`, `snapshots:publish` | View snapshots / publish and activate |
| Clients | `clients:read`, `clients:write` | View / manage client credentials |
| Sensitive Values | `sensitive_values:decrypt` | Decrypt and view sensitive config entry values via Management API |
| Audit | `audit:read` | View audit records |

Permissions are constants defined in code. New permissions are added via code changes only — they are not stored in the database.

### Roles

Roles are named collections of permissions stored in the `roles` collection. See [Data Model](Data-Model.md) for the schema.

```
Role:
  id: Guid (UUIDv7)
  name: string (unique)
  description: string?
  permissions: string[]
  version: long
  createdAt: DateTimeOffset
  createdBy: Guid
  updatedAt: DateTimeOffset
  updatedBy: Guid
```

Four default roles are seeded at startup (idempotent — skip if role name already exists). Admins can freely modify or delete any role, including defaults.

**Default roles and their permissions:**

| Permission               | Viewer | Editor | Publisher | Admin |
|--------------------------|:------:|:------:|:---------:|:-----:|
| `scopes:read`            |   x    |   x    |     x     |   x   |
| `scopes:write`           |        |        |           |   x   |
| `groups:read`            |   x    |   x    |     x     |   x   |
| `groups:write`           |        |        |           |   x   |
| `users:read`             |        |        |           |   x   |
| `users:write`            |        |        |           |   x   |
| `roles:read`             |        |        |           |   x   |
| `roles:write`            |        |        |           |   x   |
| `projects:read`          |   x    |   x    |     x     |   x   |
| `projects:write`         |        |   x    |     x     |   x   |
| `templates:read`         |   x    |   x    |     x     |   x   |
| `templates:write`        |        |   x    |     x     |   x   |
| `variables:read`         |   x    |   x    |     x     |   x   |
| `variables:write`        |        |   x    |     x     |   x   |
| `config-entries:read`    |   x    |   x    |     x     |   x   |
| `config-entries:write`   |        |   x    |     x     |   x   |
| `snapshots:read`         |   x    |   x    |     x     |   x   |
| `snapshots:publish`      |        |        |     x     |   x   |
| `clients:read`           |        |   x    |     x     |   x   |
| `clients:write`          |        |   x    |     x     |   x   |
| `sensitive_values:decrypt` |      |        |           |   x   |
| `audit:read`             |   x    |   x    |     x     |   x   |

> **Note:** `sensitive_values:decrypt` is restricted to Admin by default because decrypted secrets are high-value targets. Teams that need delegated decrypt access can create a custom role with this permission.

### Grants

A **grant** assigns a role to a user within a specific resource scope, with optional conditions restricting which configuration scope values the user can access.

```
Grant:
  resource: Guid?                      // null = system-wide, Guid = groupId
  roleId: Guid                         // references roles collection
  conditions: Dict<string, string[]>?  // null = all scope values, or filter
```

**Resource** determines where the role applies:
- `null` = system-wide (global fallback — applies to all groups)
- `Guid` = specific group only

**Conditions** optionally restrict which configuration scope values the user can access:
- `null` = unrestricted (all scope values visible)
- `{ "environment": ["Testing", "Staging"] }` = only matching scope values

Users have a `grants` array on their entity. No grants = no access.

**Examples:**

| User | Grants | Effective Access |
|------|--------|------------------|
| System Admin | `[{ resource: null, roleId: Admin }]` | All permissions everywhere, all scope values |
| Global Editor | `[{ resource: null, roleId: Editor }]` | Editor everywhere, all scope values |
| Group Editor | `[{ resource: groupA, roleId: Editor }]` | Editor in Group A only, no access elsewhere |
| Restricted | `[{ resource: groupA, roleId: Editor, conditions: { "environment": ["Testing"] } }]` | Editor in Group A, Testing scope values only |
| Mixed | `[{ resource: null, roleId: Viewer }, { resource: groupA, roleId: Publisher }]` | Publisher in Group A, Viewer everywhere else |
| No access | `[]` | Empty = no access |

### Key Rules

- **System-wide grants are a global fallback** — a system-wide Editor has Editor access in every group.
- **Effective permissions are additive** — permissions from all applicable grants (system-wide + group-specific) are unioned. The user gets the combined set.
- **No access by default** — users with an empty `grants` array have no access to anything.
- **Conditions filter scope values, not resource access** — conditions restrict which configuration scope dimension values (e.g., `environment=Production`) the user can see/edit, not which resources they can access.
- **A role cannot be deleted while in use** — the admin must reassign affected users before deleting a role.

### Condition Matching Semantics

Grant conditions use `Dictionary<string, string[]>`:

- **Multiple keys** within a single condition are combined with **AND** — the scope value must match all keys.
- **Multiple values** within a key are combined with **OR** — the scope value must match any of the listed values.
- **Conditions across grants** are **unioned** (additive) — consistent with how permissions are additive. If any applicable grant allows a scope value, the user can access it.
- **Matching is case-sensitive** — condition values must exactly match scope definition values (e.g., `"Production"` not `"production"`).

**Example:** A grant with `{ "environment": ["Testing", "Staging"], "region": ["EU"] }` allows scope values where `environment` is Testing OR Staging **AND** `region` is EU.

---

## Authentication Modes

### No Auth (`NoAuthConfigurator`)

For development and homelab deployments where authentication is unnecessary.

- Registers a custom `AuthenticationHandler` that always returns success with an admin `ClaimsPrincipal`.
- The synthetic principal represents a well-known system user (`Guid.Empty`) with a system-wide Admin grant. This is an in-memory-only principal — no corresponding document exists in the `users` collection.
- No endpoints mapped.
- PATs not available (no users to tie them to).
- All audit records use `Guid.Empty` as the actor — clearly identifiable when migrating to an authenticated mode.
- Audit record lookups that attempt to resolve `PerformedBy = Guid.Empty` should handle this gracefully (e.g., display as "System" or "No Auth User").

### Built-In (`BuiltInAuthConfigurator`)

Self-contained authentication using ASP.NET Identity with MongoDB.

**Architecture:**

- ASP.NET Identity with `IdentityUser<Guid>` stored in a separate `identity_users` collection (not the domain `users` collection). The `Id` field links the two collections.
- Two authentication schemes are registered:
  - **Cookie** (`"Cookies"`) — for browser/SPA sessions.
  - **Bearer** (`"Bearer"`) — for personal access tokens (PATs) and JWT access tokens.
- `ForwardDefaultSelector` routes requests: if an `Authorization: Bearer` header is present, use the Bearer scheme; otherwise, use the Cookie scheme. Bearer takes **strict precedence** — if a Bearer token is present but invalid, the request is rejected (no cookie fallback occurs).
- `IClaimsTransformation` loads the domain `User` from the `users` collection and enriches the `ClaimsPrincipal` with the user's grants.
- Admin seeding via `IHostedService` at startup (idempotent, password from environment variable).
- Admin-created users only. No self-registration.

**Endpoints:**

| Method | Path | Description | Auth Required |
|--------|------|-------------|---------------|
| `POST` | `/auth/login` | Cookie-based login. Validates credentials, sets `HttpOnly` session cookie. | No (accepts credentials) |
| `POST` | `/auth/logout` | Clears session cookie. | Yes |
| `POST` | `/auth/token` | JWT-based login. Returns access token + refresh token. | No (accepts credentials) |
| `POST` | `/auth/token/refresh` | Exchanges refresh token for new access token + refresh token (rotation). | No (accepts refresh token) |
| `GET` | `/auth/me` | Returns current user info. Works with both cookie and bearer auth. | Yes |

**Login request/response:**

```
POST /auth/login
Content-Type: application/json

{ "username": "admin", "password": "..." }

→ 200 OK (Set-Cookie: .GroundControl.Auth=...)
→ 401 Unauthorized (invalid credentials)
→ 423 Locked (account locked out)
```

```
POST /auth/token
Content-Type: application/json

{ "username": "admin", "password": "..." }

→ 200 OK
{
  "accessToken": "eyJhbG...",
  "refreshToken": "dGhpcyBpcyBh...",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

```
POST /auth/token/refresh
Content-Type: application/json

{ "refreshToken": "dGhpcyBpcyBh..." }

→ 200 OK
{
  "accessToken": "eyJhbG...",
  "refreshToken": "bmV3IHJlZnJl...",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
→ 401 Unauthorized (token revoked, expired, or reused)
```

### External OIDC (`ExternalAuthConfigurator`)

Integration with external identity providers via OpenID Connect.

**Architecture:**

- Three authentication schemes: Cookie, OpenID Connect, Bearer (for PATs).
- BFF (Backend-for-Frontend) pattern: GroundControl server handles the OIDC flow and issues a session cookie to the browser. OIDC tokens are stored in the encrypted session cookie and never exposed to SPA JavaScript.
- JIT (Just-In-Time) provisioning creates or maps users on first login (configurable).
- Optional claim mapping synchronizes roles from the IdP.

**Endpoints:**

| Method | Path | Description | Auth Required |
|--------|------|-------------|---------------|
| `GET` | `/auth/login/external` | Initiates OIDC challenge (redirects to IdP). | No |
| `GET` | `/auth/callback` | OIDC callback. Validates response, provisions user if needed, sets session cookie. | No (handles OIDC response) |
| `POST` | `/auth/logout` | Clears session cookie. Optionally performs OIDC front-channel logout. | Yes |
| `GET` | `/auth/me` | Returns current user info. | Yes |

**JIT Provisioning Flow:**

When a user authenticates via the external IdP for the first time:

1. **Match by ExternalId**: Look up `users` by `ExternalId` + `ExternalProvider`. If found, link and proceed.
2. **Match by email** (if `MatchByEmail = true`): Look up `users` by email. If found **and** the user does not already have a different `ExternalId` set, set `ExternalId` and `ExternalProvider`, then proceed. If the user already has a different `ExternalId`, reject the match (prevents credential confusion from email reassignment). This enables migration from Built-In to External mode.

   > **Warning:** `MatchByEmail` is intended as a **migration convenience**, not a long-term strategy. After initial migration, set `MatchByEmail = false` and rely on `ExternalId` + `ExternalProvider` matching. Email addresses can be reassigned (e.g., when an employee leaves and another inherits their email), which could link the wrong person to an existing account.
3. **Auto-create** (if `AutoCreate = true`): Create a new user in `users` with `grants: []` (no access). Set `ExternalId` and `ExternalProvider`. An admin must assign grants before the user can access anything.
4. **Reject**: If none of the above match and auto-create is disabled, return 403.

**Claim Mapping (Optional):**

When enabled, external claims are mapped to GroundControl grants on each login. The IdP becomes the source of truth for roles.

```json
{
  "ClaimMapping": {
    "RoleMapping": {
      "Claim": "roles",
      "Mappings": {
        "gc-admin": "Admin",
        "gc-publisher": "Publisher",
        "gc-editor": "Editor",
        "gc-viewer": "Viewer"
      }
    },
    "GroupRoleMapping": {
      "Enabled": false,
      "Claim": "groups",
      "Mappings": {
        "team-payments": { "groupName": "Payments", "roleName": "Editor" },
        "team-platform": { "groupName": "Platform", "roleName": "Publisher" }
      }
    }
  }
}
```

- `RoleMapping` produces system-wide grants: `{ resource: null, roleId: <matched role ID> }`.
- `GroupRoleMapping` produces group-scoped grants: `{ resource: <group ID>, roleId: <matched role ID> }`.
- Role names in mappings reference the `roles` collection by name.
- When claim mapping is **disabled** (the default), grants are managed entirely within GroundControl, even in External mode.

---

## Bearer Token Handler: PAT vs JWT

A single `"Bearer"` authentication scheme with internal routing:

1. **Token starts with `gc_pat_`** → PAT path:
   - Compute SHA-256 hash of the full token.
   - Look up in `personal_access_tokens` by `TokenHash`.
   - Validate: not revoked, not expired, owning user's `IsActive = true`.
   - Create `ClaimsPrincipal` with the user's ID.
   - If the PAT has a non-null `permissions` list, store it on the `ClaimsPrincipal` as a scope claim for downstream permission checks.

2. **Otherwise** → JWT path:
   - Validate JWT signature (HMAC-SHA256), issuer, audience, expiry.
   - Extract claims from JWT payload.
   - Create `ClaimsPrincipal`.

In both cases, `IClaimsTransformation` enriches the principal with the user's grants (resolved from the `users` collection).

---

## Personal Access Tokens (PATs)

PATs provide programmatic access for CI/CD pipelines, CLI tools, and automation scripts. Available in Built-In and External modes (not in No Auth mode).

### Characteristics

- **Format**: `gc_pat_<random>` (minimum 256 bits of entropy).
- **Storage**: SHA-256 hash stored in the `personal_access_tokens` collection. The raw token is returned only at creation time (same pattern as client secrets).
- **Permissions**: Optional scoping via `permissions` field. When `null` (default), the PAT inherits all of the owning user's permissions. When set, effective permissions are the intersection of the user's grant-derived permissions and the token's allowed list. The `permissions` field can only restrict access, never expand it.
- **Lifetime**: Configurable expiration (default 90 days, max 365 days).
- **Limits**: Configurable max tokens per user (default 10).

### Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/api/users/{userId}/tokens` | List user's PATs (metadata only, no token values). | Self or `users:write` |
| `POST` | `/api/users/{userId}/tokens` | Create a PAT. Returns the raw token once. | Self only |
| `GET` | `/api/users/{userId}/tokens/{tokenId}` | Get PAT metadata (name, prefix, expiration, last used). | Self or `users:write` |
| `DELETE` | `/api/users/{userId}/tokens/{tokenId}` | Revoke a PAT. | Self or `users:write` |

> **Note:** PAT creation is restricted to the owning user (Self only). This prevents users with `users:write` from creating tokens that inherit another user's permissions. Listing and revoking other users' PATs is allowed with `users:write` for administrative purposes.

**Create request/response:**

```
POST /api/users/{userId}/tokens
Content-Type: application/json

{ "name": "CI pipeline", "expiresInDays": 90, "permissions": ["config-entries:read", "snapshots:read"] }

→ 201 Created
{
  "id": "0192d4e0-...",
  "name": "CI pipeline",
  "token": "gc_pat_a1b2c3d4e5f6...",    ← returned only once
  "tokenPrefix": "gc_pat_a1",
  "permissions": ["config-entries:read", "snapshots:read"],
  "expiresAt": "2026-05-26T00:00:00Z",
  "createdAt": "2026-02-25T00:00:00Z"
}
```

---

## Refresh Tokens

Refresh tokens enable long-lived programmatic sessions without storing user credentials. Available in Built-In mode only.

### Characteristics

- **Storage**: SHA-256 hash in the `refresh_tokens` collection. Raw value returned at token issuance.
- **Rotation**: Each refresh token can only be used once. On use, it is revoked and a new token is issued.
- **Family tracking**: Each refresh token carries a `FamilyId` (Guid). A new `FamilyId` is generated on initial login (`POST /auth/token`). Rotated tokens inherit the same `FamilyId`.
- **Family revocation**: If a revoked refresh token is reused (possible replay attack), all tokens with the same `FamilyId` are revoked immediately. This preserves other active sessions (which have different `FamilyId` values).
- **Lifetime**: Configurable (default 7 days).
- **Cleanup**: Expired tokens are automatically removed via a TTL index.

---

## Authorization

The authorization layer is **identical regardless of authentication mode**. It uses ASP.NET Core Authorization based on the `ClaimsPrincipal` produced by the authentication handler.

Authorization operates in two layers:

1. **Permission check** — does the user have the required permission for this resource?
2. **Scope value filtering** — which configuration scope values can the user see/edit?

### Claims Transformation

`IClaimsTransformation` is the bridge between authentication and authorization. Regardless of how the user authenticated (cookie, JWT, PAT, no-auth handler), it loads the domain `User` entity from the `users` collection and enriches the `ClaimsPrincipal`:

```csharp
public class GroundControlClaimsTransformation : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // 1. Extract user ID from the principal's "sub" claim
        // 2. Load User from `users` collection
        // 3. Check IsActive (reject if inactive)
        // 4. Add "Grant" claims (one per grant: "resource:roleId" or "*:roleId" for system-wide)
        // For no-auth mode: skipped (synthetic principal already has admin claims)
    }
}
```

### Layer 1: Permission Check

Determines whether the user has the required permission for a resource, considering all applicable grants.

```csharp
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }  // e.g., "projects:write"
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement, IGroupOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        IGroupOwnedResource resource)
    {
        var user = LoadUser(context.User);
        if (HasPermission(user, resource.GroupId, requirement.Permission))
            context.Succeed(requirement);
    }
}
```

**Resolution algorithm:**

```csharp
bool HasPermission(User user, Guid? resourceGroupId, string permission)
{
    foreach (var grant in user.Grants)
    {
        // Skip grants for other groups
        if (grant.Resource != null && grant.Resource != resourceGroupId)
            continue;

        // System-wide grants (resource = null) always apply — global fallback
        var role = GetCachedRole(grant.RoleId);
        if (role.Permissions.Contains(permission))
            return true;
    }
    return false;
}
```

Permissions from all applicable grants are **unioned** (additive). A system-wide grant always applies regardless of the target group.

> **PAT scoping:** When the request is authenticated via a scoped PAT (non-null `permissions` list), an additional check applies before grant resolution: if the PAT's `permissions` list does not include the required permission, the check returns `false` immediately. This ensures PATs can only exercise permissions they were explicitly granted, without modifying the grant resolution logic.

### Layer 2: Scope Value Filtering

Restricts which configuration scope values (e.g., `environment=Production`) the user can see or edit. Only applies to operations on scoped values within config entries, variables, and snapshots — not to resource-level operations like creating a project.

```csharp
bool CanAccessScopeValue(User user, Guid? resourceGroupId, Dict<string, string> scopeValue)
{
    foreach (var grant in user.Grants)
    {
        if (grant.Resource != null && grant.Resource != resourceGroupId)
            continue;

        // null conditions = unrestricted access to all scope values
        if (grant.Conditions == null)
            return true;

        if (ConditionsMatch(grant.Conditions, scopeValue))
            return true;
    }
    return false;
}
```

Conditions are **additive** — if ANY applicable grant has null conditions, the user sees all scope values. A system-wide grant with no conditions grants unrestricted scope value access everywhere.

**How scope value filtering applies:**

| Resource | Read | Write |
|----------|------|-------|
| Config entries | Filter scoped values to only those matching conditions | Reject writes to scoped values outside conditions |
| Variables | Filter scoped values to matching conditions | Reject writes to scoped values outside conditions |
| Snapshots | Filter resolved entries to matching scope values | Publish computes full snapshot (all scopes); response shows only matching values |
| Projects, templates, groups | Not applicable (no scope dimension) | Not applicable |

### Role Caching

Roles are resolved on every permission check. To avoid repeated database queries:
- Cache roles in memory with a configurable TTL (default: 5 minutes) or change notification.
- Role updates invalidate the cache (same change stream pattern used for configuration updates).

### Authorization Matrix

| Endpoint Group | Create | Read (List/Get) | Update | Delete |
|----------------|--------|------------------|--------|--------|
| Scopes | `scopes:write` | `scopes:read` | `scopes:write` | `scopes:write` |
| Groups | `groups:write` | `groups:read` | `groups:write` | `groups:write` |
| Group Members | `groups:write` | `groups:write` | `groups:write` | `groups:write` |
| Users | `users:write` | `users:read` (list), Self (own profile) | `users:write`, Self (own profile) | `users:write` |
| Roles | `roles:write` | `roles:read` | `roles:write` | `roles:write` |
| Projects | `projects:write` | `projects:read` | `projects:write` | `projects:write` |
| Templates | `templates:write` | `templates:read` | `templates:write` | `templates:write` |
| Variables | `variables:write` | `variables:read` | `variables:write` | `variables:write` |
| Config Entries | `config-entries:write` | `config-entries:read` | `config-entries:write` | `config-entries:write` |
| Snapshots | `snapshots:publish` | `snapshots:read` | — | — |
| Clients | `clients:write` | `clients:read` | `clients:write` | `clients:write` |
| Audit Records | — | `audit:read` (scoped) | — | — |
| PATs | Self only | Self or `users:write` | — | Self or `users:write` |

All permissions are checked against the user's effective permissions for the resource's group scope. System-wide grants serve as a global fallback.

### Scoped Audit Records

Audit records are filtered based on the requesting user's grants:

| User Access | Visibility |
|-------------|------------|
| System-wide `audit:read` (no conditions) | All audit records |
| Group-scoped grants | Audit records where `GroupId` matches the user's granted groups, plus system-level records (`GroupId = null`) |
| No applicable grants | System-level records only (`GroupId = null`) |

To enable efficient filtering, audit records include a denormalized `GroupId` field:

- Group-owned entities (projects, templates, variables, config entries, snapshots, clients) → `GroupId = entity's groupId`
- System-level entities (scopes, global templates/variables) → `GroupId = null`
- User/group/role management actions → `GroupId = null`

---

## Admin Seeding

In Built-In mode, the first admin user is created automatically at startup via an `IHostedService`.

**Behavior:**

1. Check that `GroundControl:Security:AuthenticationMode` is `BuiltIn`.
2. Check that the `GroundControl:Security:Seed` section is configured.
3. Check if a user with the seed username already exists in `users`. If yes, skip (idempotent).
4. Create a user in `users` with `grants: [{ resource: null, roleId: <Admin role ID> }]`.
5. Create a corresponding identity in `identity_users` with the hashed password.
6. Log a warning: "Seed admin user created. Change the password immediately."

**Configuration:**

```json
{
  "GroundControl": {
    "Security": {
      "Seed": {
        "AdminUsername": "admin",
        "AdminEmail": "admin@local"
      }
    }
  }
}
```

The password is supplied via environment variable: `GroundControl__Security__Seed__AdminPassword`. This ensures the password never appears in configuration files.

---

## Mode Switching / Migration

### No Auth → Built-In

1. Set `GroundControl:Security:AuthenticationMode = BuiltIn` and configure the seed admin section.
2. Seed admin is created at startup.
3. Existing audit records with `Guid.Empty` as the actor remain valid and clearly identifiable.

### Built-In → External

1. Set `GroundControl:Security:AuthenticationMode = External` and configure OIDC settings.
2. Pre-map existing users: for each user in `users`, set `ExternalId` to the expected `sub` claim from the IdP, and `ExternalProvider` to the provider name (e.g., `"entra"`). This can be done via a batch script or API calls before switching.
3. Alternatively, enable `GroundControl:Security:External:JitProvisioning:MatchByEmail = true` to automatically map users on first OIDC login by matching email addresses.
4. The `identity_users` collection becomes dormant and can be cleaned up later.

### External → Built-In

1. Set `GroundControl:Security:AuthenticationMode = BuiltIn`.
2. Users retain their `users` collection entries (grants, profile).
3. Create `identity_users` entries with passwords for each user (admin action or self-service password setup flow).

---

## CSRF Protection

Cookie-based authentication requires CSRF protection to prevent cross-site request forgery.

**Strategy: Double-submit cookie pattern.**

1. The server sets a non-`HttpOnly` cookie (`XSRF-TOKEN`) containing a random token.
2. The SPA reads the cookie value from JavaScript.
3. The SPA includes the value in a custom header (`X-XSRF-TOKEN`) on every state-changing request (`POST`, `PUT`, `DELETE`).
4. The server validates that the header value matches the cookie value.

This approach works with `SameSite=Lax` cookies (required for OIDC redirect callbacks) and does not require server-side session state for CSRF tokens.

**Configuration:**

```json
{
  "GroundControl": {
    "Security": {
      "Csrf": {
        "CookieName": "XSRF-TOKEN",
        "HeaderName": "X-XSRF-TOKEN"
      }
    }
  }
}
```

---

## Configuration Reference

```json
{
  "GroundControl": {
    "Security": {
      "AuthenticationMode": "None",

      "BuiltIn": {
        "Jwt": {
          "Issuer": "GroundControl",
          "Audience": "GroundControl",
          "AccessTokenLifetime": "01:00:00",
          "RefreshTokenLifetime": "7.00:00:00"
        },
        "Cookie": {
          "Name": ".GroundControl.Auth",
          "ExpireTimeSpan": "14.00:00:00",
          "SlidingExpiration": true
        },
        "Password": {
          "RequiredLength": 12,
          "RequireDigit": true,
          "RequireUppercase": true,
          "RequireLowercase": true,
          "RequireNonAlphanumeric": false
        },
        "Lockout": {
          "MaxFailedAttempts": 5,
          "LockoutDuration": "00:15:00"
        }
      },

      "External": {
        "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
        "ClientId": "...",
        "ResponseType": "code",
        "Scopes": ["openid", "profile", "email"],
        "CallbackPath": "/auth/callback",
        "JitProvisioning": {
          "Enabled": true,
          "MatchByEmail": true,
          "AutoCreate": true
        },
        "ClaimMapping": {
          "RoleMapping": {
            "Claim": "roles",
            "Mappings": {}
          },
          "GroupRoleMapping": {
            "Enabled": false,
            "Claim": "groups",
            "Mappings": {}
          }
        }
      },

      "PersonalAccessTokens": {
        "Enabled": true,
        "MaxPerUser": 10,
        "MaxLifetimeDays": 365,
        "DefaultLifetimeDays": 90
      },

      "Seed": {
        "AdminUsername": "admin",
        "AdminEmail": "admin@local"
      },

      "Csrf": {
        "Enabled": true,
        "CookieName": "XSRF-TOKEN",
        "HeaderName": "X-XSRF-TOKEN"
      }
    }
  }
}
```

**Secrets** (supplied via environment variables, never in config files):

| Setting | Environment Variable |
|---------|---------------------|
| JWT signing key | `GroundControl__Security__BuiltIn__Jwt__Secret` |
| OIDC client secret | `GroundControl__Security__External__ClientSecret` |
| Seed admin password | `GroundControl__Security__Seed__AdminPassword` |

---

## Security Considerations

| Concern | Approach |
|---------|----------|
| Cookie security | `HttpOnly`, `Secure` (production), `SameSite=Lax`, encrypted by ASP.NET Data Protection |
| CSRF | Double-submit cookie pattern (non-HttpOnly CSRF cookie + custom header) |
| PAT entropy | Minimum 256 bits (same as client secrets) |
| PAT storage | SHA-256 hash (irreversible); raw value returned only at creation |
| PAT scoping | Optional permission whitelist; effective permissions = user grants ∩ token permissions |
| Password storage | ASP.NET Identity PBKDF2 (default) |
| JWT signing | HMAC-SHA256; signing key shared across all instances via environment variable or K8s Secret |
| Refresh tokens | SHA-256 hashed; single-use with rotation; family revocation on replay detection |
| OIDC tokens | Stored in encrypted session cookie; never exposed to SPA JavaScript |
| Rate limiting | On `/auth/login` and `/auth/token` endpoints to prevent brute-force |
| Account lockout | ASP.NET Identity lockout (configurable max failed attempts + duration) |
| Transport | TLS required in production for all auth endpoints |
