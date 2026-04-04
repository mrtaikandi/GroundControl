# Configuration Management

Commands for managing CLI configuration settings, configuration entries, and variables in GroundControl. These commands let you view and import CLI settings, create and manage scoped configuration entries attached to templates or projects, and define reusable variables at global or project scope.

---

## `config` -- Manage GroundControl CLI configuration

### `config show` -- Display effective configuration

Displays the current CLI configuration, including the server URL, authentication settings, and any other active options.

**Options:**

This command has no options.

**Examples:**

```bash
# Show the current CLI configuration
groundcontrol config show
```

---

### `config import` -- Import server configuration from a JSON file or paste

Imports CLI configuration from an external JSON source. You must specify either `--file` or `--paste`, but not both.

**Options:**

| Option    | Type   | Required | Description                                    |
|-----------|--------|----------|------------------------------------------------|
| `--file`  | string | No       | Path to a JSON configuration file              |
| `--paste` | flag   | No       | Paste JSON configuration interactively         |
| `--yes`   | flag   | No       | Skip confirmation prompt                       |

> **Note:** Exactly one of `--file` or `--paste` must be provided.

**Examples:**

```bash
# Import configuration from a file
groundcontrol config import --file ./server-config.json

# Import configuration by pasting JSON interactively
groundcontrol config import --paste

# Import from a file without confirmation
groundcontrol config import --file ./server-config.json --yes
```

---

## `config-entry` -- Manage configuration entries

### `config-entry list` -- List configuration entries

Lists configuration entries with optional filters for owner, key prefix, and sensitive value decryption.

**Options:**

| Option         | Type                    | Required | Description                                              |
|----------------|-------------------------|----------|----------------------------------------------------------|
| `--owner-id`   | Guid                    | No       | Filter by owner ID                                       |
| `--owner-type` | `Template` \| `Project` | No       | Filter by owner type                                     |
| `--key-prefix` | string                  | No       | Filter by key prefix                                     |
| `--decrypt`    | flag                    | No       | Decrypt sensitive values (otherwise they appear masked)   |

**Examples:**

```bash
# List all configuration entries
groundcontrol config-entry list

# List entries belonging to a specific template
groundcontrol config-entry list --owner-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 --owner-type Template

# List entries with a key prefix and decrypt sensitive values
groundcontrol config-entry list --key-prefix "Database:" --decrypt
```

---

### `config-entry get` -- Get a configuration entry by ID

Retrieves a single configuration entry by its unique identifier.

**Arguments:**

| Name | Type | Required | Description                  |
|------|------|----------|------------------------------|
| `id` | Guid | Yes      | The configuration entry ID   |

**Options:**

| Option      | Type | Required | Description                                              |
|-------------|------|----------|----------------------------------------------------------|
| `--decrypt` | flag | No       | Decrypt sensitive values (otherwise they appear masked)   |

**Examples:**

```bash
# Get a configuration entry
groundcontrol config-entry get a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Get a configuration entry and reveal sensitive values
groundcontrol config-entry get a1b2c3d4-e5f6-7890-abcd-ef1234567890 --decrypt
```

---

### `config-entry create` -- Create a new configuration entry

Creates a new configuration entry attached to a template or project. Scoped values can be provided inline using repeatable `--value` options or as a JSON array via `--values-json`.

**Options:**

| Option           | Type                    | Required | Description                                                        |
|------------------|-------------------------|----------|--------------------------------------------------------------------|
| `--key`          | string                  | Yes      | The configuration key (e.g., `Database:ConnectionString`)          |
| `--owner-id`     | Guid                    | Yes      | The owning template or project ID                                  |
| `--owner-type`   | `Template` \| `Project` | Yes      | The owner type                                                     |
| `--value-type`   | string                  | Yes      | The value type name (e.g., `String`, `Int32`, `Boolean`)           |
| `--sensitive`    | flag                    | No       | Whether the entry contains sensitive data                          |
| `--description`  | string                  | No       | The entry description                                              |
| `--value`        | string[]                | No       | Scoped value (repeatable, e.g., `"default=myval"`)                 |
| `--values-json`  | string                  | No       | Scoped values as a JSON array                                      |

**Examples:**

```bash
# Create an entry with a single default value
groundcontrol config-entry create \
  --key "Database:ConnectionString" \
  --owner-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --owner-type Template \
  --value-type String \
  --value "default=Server=localhost;Database=appdb"

# Create an entry with multiple scoped values
groundcontrol config-entry create \
  --key "Database:Host" \
  --owner-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --owner-type Project \
  --value-type String \
  --value "default=localhost" \
  --value "Environment:prod=prod-db.example.com" \
  --value "Environment:staging=staging-db.example.com"

# Create an entry using a JSON array for scoped values
groundcontrol config-entry create \
  --key "Cache:TTL" \
  --owner-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --owner-type Template \
  --value-type Int32 \
  --values-json '[{"scope": "default", "value": "300"}, {"scope": "Environment:prod", "value": "3600"}]'

# Create a sensitive configuration entry
groundcontrol config-entry create \
  --key "Secrets:ApiKey" \
  --owner-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --owner-type Project \
  --value-type String \
  --sensitive \
  --description "Third-party API key" \
  --value "default=sk-test-abc123" \
  --value "Environment:prod=sk-live-xyz789"
```

---

### `config-entry update` -- Update a configuration entry

Updates an existing configuration entry. Use the `--version` option for optimistic concurrency to ensure you are updating the expected revision.

**Arguments:**

| Name | Type | Required | Description                  |
|------|------|----------|------------------------------|
| `id` | Guid | Yes      | The configuration entry ID   |

**Options:**

| Option          | Type     | Required | Description                                                |
|-----------------|----------|----------|------------------------------------------------------------|
| `--value-type`  | string   | No       | The new value type                                         |
| `--sensitive`   | flag     | No       | Whether the entry contains sensitive data                  |
| `--description` | string   | No       | The new description                                        |
| `--value`       | string[] | No       | Scoped value (repeatable)                                  |
| `--values-json` | string   | No       | Scoped values as a JSON array                              |
| `--version`     | long     | No       | Expected version for optimistic concurrency                |

**Examples:**

```bash
# First, get the current entry to obtain the version
groundcontrol config-entry get a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Update the entry description and values using the version from the get response
groundcontrol config-entry update a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --description "Updated connection string for v2 schema" \
  --value "default=Server=localhost;Database=appdb_v2" \
  --version 3

# Update the value type
groundcontrol config-entry update a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --value-type Boolean \
  --version 4
```

---

### `config-entry delete` -- Delete a configuration entry

Deletes a configuration entry. Use `--version` for optimistic concurrency and `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description                  |
|------|------|----------|------------------------------|
| `id` | Guid | Yes      | The configuration entry ID   |

**Options:**

| Option      | Type | Required | Description                                   |
|-------------|------|----------|-----------------------------------------------|
| `--version` | long | No       | Expected version for optimistic concurrency   |
| `--yes`     | flag | No       | Skip confirmation prompt                      |

**Examples:**

```bash
# Delete with confirmation prompt
groundcontrol config-entry delete a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Delete using optimistic concurrency and skip confirmation
groundcontrol config-entry delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --version 3 --yes
```

---

## `variable` -- Manage variables

### `variable list` -- List variables

Lists variables with optional filters for scope, group, project, and sensitive value decryption.

**Options:**

| Option         | Type                    | Required | Description                                              |
|----------------|-------------------------|----------|----------------------------------------------------------|
| `--scope`      | `Global` \| `Project`   | No       | Filter by scope                                          |
| `--group-id`   | Guid                    | No       | Filter by group ID                                       |
| `--project-id` | Guid                    | No       | Filter by project ID                                     |
| `--decrypt`    | flag                    | No       | Decrypt sensitive values (otherwise they appear masked)   |

**Examples:**

```bash
# List all variables
groundcontrol variable list

# List global variables
groundcontrol variable list --scope Global

# List variables for a specific project and decrypt sensitive values
groundcontrol variable list --project-id b2c3d4e5-f6a7-8901-bcde-f12345678901 --decrypt
```

---

### `variable get` -- Get a variable by ID

Retrieves a single variable by its unique identifier.

**Arguments:**

| Name | Type | Required | Description      |
|------|------|----------|------------------|
| `id` | Guid | Yes      | The variable ID  |

**Options:**

| Option      | Type | Required | Description                                              |
|-------------|------|----------|----------------------------------------------------------|
| `--decrypt` | flag | No       | Decrypt sensitive values (otherwise they appear masked)   |

**Examples:**

```bash
# Get a variable
groundcontrol variable get b2c3d4e5-f6a7-8901-bcde-f12345678901

# Get a variable and reveal sensitive values
groundcontrol variable get b2c3d4e5-f6a7-8901-bcde-f12345678901 --decrypt
```

---

### `variable create` -- Create a new variable

Creates a new variable at global or project scope. Scoped values can be provided inline using repeatable `--value` options or as a JSON array via `--values-json`.

**Options:**

| Option          | Type                  | Required | Description                                                |
|-----------------|-----------------------|----------|------------------------------------------------------------|
| `--name`        | string                | Yes      | The variable name                                          |
| `--scope`       | `Global` \| `Project` | Yes      | The variable scope                                         |
| `--group-id`    | Guid                  | No       | The group ID (for Global scope)                            |
| `--project-id`  | Guid                  | No       | The project ID (for Project scope)                         |
| `--sensitive`   | flag                  | No       | Whether the variable contains sensitive data               |
| `--description` | string                | No       | The variable description                                   |
| `--value`       | string[]              | No       | Scoped value (repeatable, e.g., `"default=myval"`)         |
| `--values-json` | string                | No       | Scoped values as a JSON array                              |

**Examples:**

```bash
# Create a global variable with a default value
groundcontrol variable create \
  --name "SmtpHost" \
  --scope Global \
  --group-id c3d4e5f6-a7b8-9012-cdef-123456789012 \
  --value "default=smtp.example.com"

# Create a project-scoped variable with multiple scoped values
groundcontrol variable create \
  --name "ApiBaseUrl" \
  --scope Project \
  --project-id b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --value "default=http://localhost:5000" \
  --value "Environment:prod=https://api.example.com" \
  --value "Environment:staging=https://staging-api.example.com"

# Create a sensitive variable using JSON values
groundcontrol variable create \
  --name "DbPassword" \
  --scope Project \
  --project-id b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --sensitive \
  --description "Database password" \
  --values-json '[{"scope": "default", "value": "devpass123"}, {"scope": "Environment:prod", "value": "s3cur3-pr0d-p@ss"}]'
```

---

### `variable update` -- Update a variable

Updates an existing variable. Use the `--version` option for optimistic concurrency to ensure you are updating the expected revision.

**Arguments:**

| Name | Type | Required | Description      |
|------|------|----------|------------------|
| `id` | Guid | Yes      | The variable ID  |

**Options:**

| Option          | Type     | Required | Description                                                |
|-----------------|----------|----------|------------------------------------------------------------|
| `--sensitive`   | flag     | No       | Whether the variable contains sensitive data               |
| `--description` | string   | No       | The new description                                        |
| `--value`       | string[] | No       | Scoped value (repeatable)                                  |
| `--values-json` | string   | No       | Scoped values as a JSON array                              |
| `--version`     | long     | No       | Expected version for optimistic concurrency                |

**Examples:**

```bash
# First, get the current variable to obtain the version
groundcontrol variable get b2c3d4e5-f6a7-8901-bcde-f12345678901

# Update the variable values using the version from the get response
groundcontrol variable update b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --description "Updated SMTP host for new mail provider" \
  --value "default=smtp.newprovider.com" \
  --value "Environment:prod=smtp-prod.newprovider.com" \
  --version 2

# Mark a variable as sensitive
groundcontrol variable update b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --sensitive \
  --version 3
```

---

### `variable delete` -- Delete a variable

Deletes a variable. Use `--version` for optimistic concurrency and `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description      |
|------|------|----------|------------------|
| `id` | Guid | Yes      | The variable ID  |

**Options:**

| Option      | Type | Required | Description                                   |
|-------------|------|----------|-----------------------------------------------|
| `--version` | long | No       | Expected version for optimistic concurrency   |
| `--yes`     | flag | No       | Skip confirmation prompt                      |

**Examples:**

```bash
# Delete with confirmation prompt
groundcontrol variable delete b2c3d4e5-f6a7-8901-bcde-f12345678901

# Delete using optimistic concurrency and skip confirmation
groundcontrol variable delete b2c3d4e5-f6a7-8901-bcde-f12345678901 --version 2 --yes
```
