# Variables

Variables are named placeholders that get interpolated into configuration entry values at snapshot publish time. They let you keep one source of truth for any value that appears in many entries — connection-string prefixes, API endpoints, shared secrets — and change it in one place instead of editing every entry that uses it.

This page covers what a variable looks like, how its visibility is determined, how a placeholder gets resolved, and the edge cases you need to know about.

## Anatomy of a variable

A variable has a name, an ownership tier, and a list of values. Each value is qualified by zero or more scope dimensions.

| Field | Type | Purpose |
|---|---|---|
| `name` | string | The key used in `{{name}}` placeholders. Case-insensitive within its uniqueness key. |
| `description` | string? | Optional human-readable note. |
| `scope` | `Global` \| `Project` | Ownership tier. See [Ownership tiers](#ownership-tiers). |
| `groupId` | Guid? | For `Global` variables only. `null` means system-wide; otherwise the variable belongs to that group. Forbidden on `Project` variables. |
| `projectId` | Guid? | Required on `Project` variables; forbidden on `Global` variables. |
| `values` | `ScopedValue[]` | One or more scoped value variants. See [Scoped values](#scoped-values). |
| `isSensitive` | bool | Encrypts at rest, masks as `***` in API responses, and propagates sensitivity to any snapshot entry that interpolates the variable. |
| `version` | long | Optimistic-concurrency token. Required on update/delete via `If-Match`. |

The full field list including audit timestamps is in [Domain Model — Variable](../design-docs/Domain-Model.md#variable).

## Ownership tiers

The `scope` field puts a variable in one of two tiers:

### Global

`scope = Global`. Used to define values shared across many projects.

`groupId` controls visibility:

- **`groupId = null`** — system-wide global. Every project, in every group (and ungrouped projects), can resolve this variable.
- **`groupId = X`** — group-owned global. Only projects whose `Project.GroupId` equals `X` can resolve it.

`projectId` must be `null` on global variables.

### Project

`scope = Project`. Used to override a global variable's value for one specific project, or to define a value that only that project needs.

`projectId` is required and must reference an existing project. `groupId` must be `null` — a project variable inherits its group through the project.

A project variable with the same `name` as a global variable shadows the global for that project (see [Two-tier lookup](#two-tier-lookup)).

## Scoped values

Each entry in `values` represents the variable's value for a specific scope combination:

```json
{
  "scopes": { "Environment": "prod", "Region": "eu" },
  "value": "https://api.eu.example.com"
}
```

- `scopes` is a dimension → value map. Dimensions must already exist in the Scopes registry; values must be in the dimension's allowed-values set. Validated on write by [`CreateVariableValidator`](../../src/GroundControl.Api/Features/Variables/CreateVariableValidator.cs).
- An empty `scopes` map (`{}`) marks the **unscoped default** — used when no scoped variant matches the requesting client.
- `value` is always a string. The interpolation rule below treats it as a literal: variable values **cannot themselves contain `{{...}}`** placeholders. Nested interpolation is rejected on write.

A single variable typically holds one unscoped default plus one variant per environment/region/tier combination it needs to differ on.

## How a placeholder resolves

When a snapshot is published for a project, every config entry value is scanned for `{{name}}` placeholders. The publish pipeline **fans out** each scoped value across the scope tuples its referenced variables touch, resolving each variable per tuple. The snapshot stores the materialized per-tuple values; the client read path picks one tuple at request time using the same scope-matching rules without any further interpolation.

### Fan-out at publish

For each source scoped value on the entry:

1. **Scan placeholders.** Every `{{name}}` is mapped to a variable via the [two-tier lookup](#two-tier-lookup) below.
2. **Generate target tuples.** The set of scope dimensions referenced by the resolved variables forms the target dim-space; per-dimension domain is the union of distinct values plus an "unspecified" axis. The cartesian product yields the targets to materialize.
3. **Merge with the source's own scope.** A target whose dimension value conflicts with the source scope on the same dimension is dropped; surviving targets become the final scope tuple.
4. **Substitute per target.** [`VariableInterpolator`](../../src/GroundControl.Api/Features/Snapshots/VariableInterpolator.cs) is invoked once per final tuple. Each placeholder is resolved against that tuple by [`ScopeResolver`](../../src/GroundControl.Api/Shared/Resolvers/ScopeResolver.cs) (most-specific scope wins, falling back to the variable's unscoped default).

After fan-out across every source value of the entry, emissions for the same final scope tuple are deduplicated:

- Within one source value, the most-specific target wins.
- Across source values, an emission whose source had a more specific scope tuple wins — this is the **explicit-wins rule**: a literal scoped value on the entry overrides a fan-out emission from a default-scoped sibling that references a variable.

The orchestration lives in [`ResolvedEntryBuilder`](../../src/GroundControl.Api/Features/Snapshots/ResolvedEntryBuilder.cs); target enumeration is [`TargetTupleBuilder`](../../src/GroundControl.Api/Features/Snapshots/TargetTupleBuilder.cs).

#### Practical effect

A scopeless config entry like `MyConfigEntry = "{{MyVariable}}"` referencing a variable with `Environment = dev/prod` tuples produces one resolved snapshot value per environment plus the unscoped default. You don't have to redeclare scope tuples on every entry that uses a scoped variable.

### Two-tier lookup

For each placeholder `{{name}}`, the publisher looks the name up in two tiers:

1. The project's project-scope variables.
2. The project's visible globals.

The first tier that contains the name supplies the variable; the global tier is a fallback, not a merge. Within the variable, scope-matching against a target tuple uses the same most-specific-wins algorithm described in [Scope resolution within a tier](#scope-resolution-within-a-tier).

### Scope resolution within a tier

Within a single variable's `values` list, [`ScopeResolver`](../../src/GroundControl.Api/Shared/Resolvers/ScopeResolver.cs) picks one variant for the target tuple:

1. Filter to candidates whose `scopes` map is a **full match** of the target — every dimension in the candidate must equal the target's value (case-insensitive on the dimension name, exact on the value).
2. Of the matches, the candidate with the **most dimensions** wins.
3. If no scoped candidate matches, fall back to the unscoped default (`scopes = {}`).
4. If there isn't even an unscoped default, the variable contributes no value for this target tuple and the placeholder is **unresolved** — the publish fails with the offending name reported back ([strict-unresolved policy](#strict-unresolved-policy)).

A tie at the same specificity logs a warning and returns the first match — design your scoped values so combinations don't collide.

### Strict-unresolved policy

A placeholder is considered unresolved (and blocks publish with HTTP 422) under either condition:

- The name does not match any variable in the project lookup nor the global lookup.
- The variable exists but `ScopeResolver` returns no match for at least one target tuple required by fan-out — typically a variable with no unscoped default referenced by an entry whose targets include the empty tuple.

To unblock, add a default to the variable, or scope the entry more tightly so its targets only span tuples the variable covers.

### Visibility from a project's perspective

For a project `P` in group `G`, the variables visible at publish time are:

| Source | Visible? |
|---|---|
| Project variables where `projectId = P.id` | Always |
| Global variables where `groupId = G` | Yes |
| Global variables where `groupId = null` (system-wide) | Yes |
| Global variables where `groupId = some other group` | **No** |
| Project variables on a different project | **No** |

Implemented by [`VariableStore.GetGlobalVariablesForGroupAsync`](../../src/GroundControl.Persistence.MongoDb/Stores/VariableStore.cs) and [`SnapshotResolver.ResolveAndInterpolateAsync`](../../src/GroundControl.Api/Features/Snapshots/SnapshotResolver.cs).

## Sensitivity

Setting `isSensitive = true` does three things:

1. **Encryption at rest** — values are encrypted by `SensitiveSourceValueProtector` before being written to MongoDB.
2. **Masking on read** — API responses replace each value with `***` unless the caller has the `sensitive_values:decrypt` permission and adds `?decrypt=true`.
3. **Sensitivity propagation** — any snapshot config entry that interpolates a sensitive variable is itself treated as sensitive. The flag flips on the resolved entry even if the entry was authored as non-sensitive.

The mask sentinel `***` is reserved: you cannot save a sensitive variable whose plaintext value is literally `***` (the validator rejects it) — it would otherwise be indistinguishable from a masked read.

## Choosing the right tier

| You want… | Use |
|---|---|
| One value usable by every project in the system | `Global`, `groupId = null` |
| One value shared across every project in a single group | `Global`, `groupId = <group>` |
| A per-project tweak of a shared value (same name) | `Project` variable with the same `name` as the global |
| A value only one project ever uses | `Project` variable, no global counterpart |
| Different values per environment but the same name everywhere | One variable with multiple `ScopedValue` entries (`{Environment: prod}`, `{Environment: staging}`, plus an unscoped default) |
| Sharing a single value across **two specific groups** but not others | Not directly supported — either make it system-wide and accept the broader visibility, or duplicate it as a group-owned global in each group |

## Uniqueness rules

Enforced by partial unique indexes (case-insensitive) in [`VariableConfiguration`](../../src/GroundControl.Persistence.MongoDb/Conventions/VariableConfiguration.cs):

- `(scope=Global, groupId, name)` is unique. Two globals can share a name only if they have different `groupId`s (including `null`).
- `(scope=Project, projectId, name)` is unique.

`name` is treated case-insensitively for both uniqueness and placeholder lookup.

## Sharp edges

- **Same name at system-wide and group tier.** A `Global` variable with `groupId = null` and another `Global` variable with `groupId = X` are both stored — the unique index allows it because `groupId` differs. From a project in group `X`, both end up in the same lookup dictionary keyed by name, so whichever the dictionary build encounters last wins. The result is **order-dependent**. Don't rely on this for project-specific overrides — use a `Project`-scope variable instead.
- **No multi-group sharing.** There is no link table, no `groupId[]`, and no template-style attachment. A variable belongs to exactly one tier (system-wide or one group, or one project).
- **Variables can't reference variables.** `{{...}}` is rejected on write inside variable values; only config-entry values may contain placeholders.
- **Resolution is publish-time, not write-time.** A config entry can be saved with `{{Foo}}` even if `Foo` doesn't exist yet. The publish call is what fails when the placeholder can't be resolved.
- **Tied scope specificity.** If two scoped values in the same variable match a client with the same dimension count, you get a warning log and a non-deterministic pick. Make scope combinations unambiguous.
- **Snapshot size grows with fan-out.** A scopeless entry that references a variable with many scope tuples produces one snapshot value per tuple. Combined with multi-dimensional variables this expands cartesian-style. The 16MB BSON snapshot limit catches runaway expansion at publish — keep multi-dimensional variables narrow when an entry references several of them.

## Worked examples

### Shared API endpoint with environment overrides

One system-wide variable, used by every project, varying by environment:

```bash
curl -X POST http://localhost:8080/api/variables \
  -H "Content-Type: application/json" \
  -H "api-version: 1.0" \
  -d '{
    "name": "ApiBase",
    "scope": "Global",
    "isSensitive": false,
    "values": [
      { "scopes": {}, "value": "https://api.example.com" },
      { "scopes": { "Environment": "staging" }, "value": "https://api.staging.example.com" },
      { "scopes": { "Environment": "prod" }, "value": "https://api.example.com" }
    ]
  }'
```

In a config entry:

```json
{ "key": "Endpoints:Api", "valueType": "String",
  "values": [{ "value": "{{ApiBase}}/v1" }] }
```

The entry has a single scopeless source value. At publish, fan-out expands it into three resolved snapshot values — one per `Environment` tuple the variable touches plus the unscoped default:

| Snapshot scope | Resolved value |
|---|---|
| `{}` | `https://api.example.com/v1` |
| `{Environment: staging}` | `https://api.staging.example.com/v1` |
| `{Environment: prod}` | `https://api.example.com/v1` |

A client bound to `{Environment: staging}` is then served `https://api.staging.example.com/v1` straight from the snapshot — no further interpolation runs.

### Group-owned secret with a per-project override

A group-owned global database connection string:

```json
{
  "name": "PrimaryDb",
  "scope": "Global",
  "groupId": "<billing-group-id>",
  "isSensitive": true,
  "values": [
    { "scopes": {}, "value": "Server=db.billing.internal;Database=core;" },
    { "scopes": { "Environment": "prod" }, "value": "Server=prod-db.billing.internal;Database=core;Encrypt=True;" }
  ]
}
```

One project in that group needs to point at a dedicated read-replica. Define a project variable with the same name:

```json
{
  "name": "PrimaryDb",
  "scope": "Project",
  "projectId": "<reports-project-id>",
  "isSensitive": true,
  "values": [
    { "scopes": { "Environment": "prod" }, "value": "Server=prod-db-reports.billing.internal;Database=core;Encrypt=True;ApplicationIntent=ReadOnly;" }
  ]
}
```

The reports project in `prod` resolves `{{PrimaryDb}}` to the read-replica string. In any other environment the project variable has no matching scope, so resolution falls back to the global's unscoped default. Other projects in the same group are unaffected — they keep using the global value.

## Related

- [Core Concepts — Variables](concepts.md#variables) — short conceptual overview
- [API Reference — Variables](api/endpoints.md#variables) — endpoint shapes
- [CLI — `variable` commands](../cli/configuration.md#variable-----manage-variables)
- [Domain Model — Variable](../design-docs/Domain-Model.md#variable) — full field reference
- [Data Model — `variables`](../design-docs/Data-Model.md#variables) — persistence layout and indexes
