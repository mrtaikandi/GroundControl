# Variables

Variables are named values you can reference inside configuration entries using `{{name}}` placeholders. Use them when the same value shows up in many entries (a connection-string prefix, an API endpoint, a shared secret) so you can change it in one place and have every entry that uses it pick up the new value the next time you publish.

This page covers what a variable looks like, who can see it, how its value gets baked into a published snapshot, and the edge cases worth knowing.

> **Whitespace inside the braces is allowed.** `{{ApiBase}}`, `{{ ApiBase }}`, `{{\tApiBase\t}}` all resolve to the same variable. Whitespace is stripped from the captured name before lookup but preserved in the original token if the placeholder fails to resolve.

## Anatomy of a variable

A variable has a name, an ownership tier, and one or more values. Each value can be qualified with scope dimensions like `Environment` or `Region`.

| Field | Type | Purpose |
|---|---|---|
| `name` | string | The key used in `{{name}}` placeholders. Case-insensitive. |
| `description` | string? | Optional human-readable note. |
| `scope` | `Global` \| `Project` | Ownership tier. See [Ownership tiers](#ownership-tiers). |
| `groupId` | Guid? | For `Global` variables only. `null` means system-wide; otherwise the variable belongs to that group. Forbidden on `Project` variables. |
| `projectId` | Guid? | Required on `Project` variables; forbidden on `Global` variables. |
| `values` | `ScopedValue[]` | One or more scoped values. See [Scoped values](#scoped-values). |
| `isSensitive` | bool | Encrypts the value at rest, masks it as `***` in API responses, and marks any entry that uses it as sensitive too. |
| `version` | long | Used for optimistic concurrency. Required on update/delete via `If-Match`. |

The full field list including audit timestamps is in [Domain Model — Variable](../design-docs/Domain-Model.md#variable).

## Ownership tiers

The `scope` field puts a variable in one of two tiers.

### Global

`scope = Global`. Used for values shared across many projects.

`groupId` controls who can see it:

- **`groupId = null`** is a system-wide global. Every project in every group, including projects with no group, can resolve it.
- **`groupId = X`** is a group-owned global. Only projects whose `Project.GroupId` equals `X` can resolve it.

`projectId` must be `null` on global variables.

### Project

`scope = Project`. Used to override a global variable's value for a specific project, or to define a value that only that project needs.

`projectId` is required and must reference an existing project. `groupId` must be `null`. A project variable inherits its group through the project.

A project variable with the same `name` as a global variable shadows the global for that project (see [Two-tier lookup](#two-tier-lookup)).

## Scoped values

Each entry in `values` is a value for a specific scope combination:

```json
{
  "scopes": { "Environment": "prod", "Region": "eu" },
  "value": "https://api.eu.example.com"
}
```

- `scopes` is a dimension-to-value map. Dimensions must already exist in the Scopes registry, and values must be in the dimension's allowed-values set. The server validates this on write.
- An empty `scopes` map (`{}`) is the **unscoped default**. It is used when no scoped variant matches the requesting client.
- `value` is always a string. Variable values cannot themselves contain `{{...}}` placeholders. The server rejects nested interpolation on write.

A typical variable holds one unscoped default plus one variant per environment, region, or tier it needs to differ on.

## How a variable gets used

When you publish a snapshot, the server scans every config entry value for `{{name}}` placeholders and bakes the resolved values straight into the snapshot. From that point on, clients just read pre-resolved values; no further interpolation runs at request time.

### Scoped variables propagate to scopeless entries

If a config entry has only a default value but references a scoped variable, the server expands the entry into one resolved value per scope the variable covers. You don't have to repeat the scope tuples on the entry itself.

For example, given this variable:

| `scopes` | `value` |
|---|---|
| `{}` | `https://api.example.com` |
| `{Environment: dev}` | `https://api.dev.example.com` |
| `{Environment: prod}` | `https://api.prod.example.com` |

And this scopeless entry:

```json
{ "key": "ApiUrl", "values": [{ "value": "{{ApiBase}}" }] }
```

The published snapshot for that entry contains three values:

| `scopes` | resolved value |
|---|---|
| `{}` | `https://api.example.com` |
| `{Environment: dev}` | `https://api.dev.example.com` |
| `{Environment: prod}` | `https://api.prod.example.com` |

A client bound to `{Environment: dev}` reads `https://api.dev.example.com` directly from the snapshot.

### Two-tier lookup

For each placeholder `{{name}}`, the server looks the name up in two tiers:

1. The project's project-scope variables.
2. The project's visible globals (system-wide first, then the project's group).

The first tier that contains the name supplies the variable. The global tier is a fallback, not a merge.

### Most-specific scope wins

Within a single variable's `values` list, the server picks the variant whose `scopes` map is fully contained in the target scope and has the most dimensions in common. If nothing matches, it falls back to the unscoped default.

If two scoped values are equally specific for the same target, the result is unpredictable. Design your scoped values so combinations don't collide.

### Literal scoped values override variables

If you set a scoped value on the entry itself, it always wins over a value the publisher would have produced from a referenced variable. So an entry that authors `Environment: prod = "literal-prod-value"` keeps that literal even when the variable it references would have produced something different for `prod`.

### When publish fails

Publish blocks with a clear 422 error if any placeholder cannot resolve:

- The name doesn't match any variable in either tier (typo, deleted variable).
- The variable exists but has no value for one of the scope combinations the entry needs (typically a variable with no unscoped default, referenced by an entry that needs a default).

Fix it by adding the missing variable, adding an unscoped default to the variable, or scoping the entry tightly enough that only covered combinations are needed.

### Visibility from a project's perspective

For a project `P` in group `G`, the variables visible at publish time are:

| Source | Visible? |
|---|---|
| Project variables where `projectId = P.id` | Always |
| Global variables where `groupId = G` | Yes |
| Global variables where `groupId = null` (system-wide) | Yes |
| Global variables where `groupId = some other group` | **No** |
| Project variables on a different project | **No** |

## Sensitivity

Setting `isSensitive = true` does three things:

1. **Encryption at rest.** Values are encrypted before being written to MongoDB.
2. **Masking on read.** API responses replace each value with `***` unless the caller has the `sensitive_values:decrypt` permission and adds `?decrypt=true`.
3. **Sensitivity propagation.** Any snapshot entry that uses a sensitive variable becomes sensitive itself, even if the entry was authored as non-sensitive.

The mask sentinel `***` is reserved. You cannot save a sensitive variable whose plaintext value is literally `***`. The validator rejects it because it would be indistinguishable from a masked read.

## Choosing the right tier

| You want… | Use |
|---|---|
| One value usable by every project in the system | `Global`, `groupId = null` |
| One value shared across every project in a single group | `Global`, `groupId = <group>` |
| A per-project tweak of a shared value (same name) | `Project` variable with the same `name` as the global |
| A value only one project ever uses | `Project` variable, no global counterpart |
| Different values per environment but the same name everywhere | One variable with multiple `ScopedValue` entries (one per environment), plus an unscoped default |
| Sharing a single value across **two specific groups** but not others | Not directly supported. Either make it system-wide and accept the broader visibility, or duplicate it as a group-owned global in each group. |

## Uniqueness rules

The server enforces these constraints (case-insensitive on `name`):

- Two `Global` variables can share a name only if they have different `groupId`s (including `null`).
- Two `Project` variables can share a name only if they belong to different projects.

`name` is case-insensitive everywhere it is looked up.

## Things to watch out for

- **System-wide and group globals with the same name.** A `Global` variable with `groupId = null` and another with `groupId = X` are both valid because their `groupId`s differ. From a project in group `X`, both end up in the lookup keyed by name and the result is unpredictable. Don't rely on this for project-specific overrides; use a `Project`-scope variable instead.
- **No multi-group sharing.** A variable belongs to exactly one tier (system-wide, one group, or one project). There is no way to share it across two specific groups without duplication.
- **Variables can't reference variables.** `{{...}}` is rejected on write inside variable values. Only config entry values may contain placeholders.
- **Resolution is publish-time, not write-time.** A config entry can be saved with `{{Foo}}` even if `Foo` doesn't exist yet. The publish call fails when the placeholder can't be resolved.
- **Tied scope specificity.** If two scoped values in the same variable are equally specific for a request, the pick is unpredictable. Design combinations so they are unambiguous.
- **Snapshots can grow when many entries reference scoped variables.** Each scopeless entry that uses a scoped variable produces one snapshot value per scope. Multi-dimensional variables expand into a larger combination set. The 16 MB snapshot limit catches runaway expansion at publish time, but keep it in mind for variables that span many dimensions.

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

The entry has one value with no scope on the entry itself. When you publish, the server expands it across the variable's environments:

| Snapshot scope | Resolved value |
|---|---|
| `{}` | `https://api.example.com/v1` |
| `{Environment: staging}` | `https://api.staging.example.com/v1` |
| `{Environment: prod}` | `https://api.example.com/v1` |

A client bound to `{Environment: staging}` is served `https://api.staging.example.com/v1` directly from the snapshot.

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

The reports project in `prod` resolves `{{PrimaryDb}}` to the read-replica string. In any other environment, the project variable has no matching scope, so resolution falls back to the global's unscoped default. Other projects in the same group are unaffected. They keep using the global value.

## Related

- [Core Concepts — Variables](concepts.md#variables) — short conceptual overview
- [API Reference — Variables](api/endpoints.md#variables) — endpoint shapes
- [CLI — `variable` commands](../cli/configuration.md#variable-----manage-variables)
- [Domain Model — Variable](../design-docs/Domain-Model.md#variable) — full field reference
- [Data Model — `variables`](../design-docs/Data-Model.md#variables) — persistence layout and indexes
