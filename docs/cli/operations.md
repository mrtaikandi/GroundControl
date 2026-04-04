# Operations

Commands for managing clients, testing configuration resolution, publishing snapshots, and viewing audit records.

---

## `client` -- Manage clients

Clients are issued credentials to consume configuration from a project.

### `client list` -- List all clients for a project

List all clients that belong to a given project.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |

**Examples:**

```bash
# List all clients for a project
groundcontrol client list --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `client get` -- Get a client by ID

Retrieve the details of a specific client.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The client ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |

**Examples:**

```bash
# Get a specific client
groundcontrol client get b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `client create` -- Create a new client

Create a new client with optional scope assignments and expiration.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |
| `--name` | string | Yes | The client name |
| `--scopes` | string | No | Comma-separated scope assignments (e.g., `Environment=prod,Region=us-east`) |
| `--expires-at` | DateTimeOffset | No | Expiration timestamp in ISO 8601 format |

**Examples:**

```bash
# Create a client with scope assignments and an expiration date
groundcontrol client create \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --name "production-api-server" \
  --scopes "Environment=prod,Region=us-east" \
  --expires-at "2027-01-01T00:00:00Z"
```

---

### `client update` -- Update a client

Update an existing client. Requires the current version for optimistic concurrency.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The client ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |
| `--name` | string | No | The new client name |
| `--is-active` | bool | No | Whether the client is active |
| `--expires-at` | DateTimeOffset | No | The new expiration timestamp (ISO 8601) |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the client to find its current version
groundcontrol client get b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Then update using the version from the response
groundcontrol client update b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --name "production-api-server-v2" \
  --is-active false \
  --version 3
```

---

### `client delete` -- Delete a client

Delete a client permanently. Use `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The client ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete a client, skipping confirmation
groundcontrol client delete b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --version 3 \
  --yes
```

---

## `client-config` -- Test client configuration resolution

### `client-config get` -- Fetch resolved configuration for a client

Fetch the resolved configuration that a specific client would receive. This is useful for testing and verifying that scope assignments and configuration entries produce the expected result before deploying to a real consumer.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--client-id` | Guid | Yes | The client ID to authenticate with |
| `--client-secret` | string | Yes | The client secret to authenticate with |

**Examples:**

```bash
# After creating a client, use the returned credentials to test resolution
groundcontrol client create \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --name "test-client" \
  --scopes "Environment=staging,Region=eu-west"

# Use the client-id and client-secret from the create response
groundcontrol client-config get \
  --client-id c3d4e5f6-a7b8-9012-cdef-123456789012 \
  --client-secret "returned-secret-from-create"
```

---

## `snapshot` -- Manage snapshots

Snapshots capture a point-in-time copy of a project's resolved configuration. They are immutable once published and can be used for auditing or rollback reference.

### `snapshot list` -- List snapshots for a project

List all snapshots that have been published for a project.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |

**Examples:**

```bash
# List all snapshots for a project
groundcontrol snapshot list --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `snapshot get` -- Get a snapshot by ID

Retrieve the full contents of a snapshot, optionally decrypting sensitive values.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The snapshot ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID |
| `--decrypt` | flag | No | Decrypt sensitive values |

**Examples:**

```bash
# Get a snapshot with decrypted values
groundcontrol snapshot get d4e5f6a7-b8c9-0123-def0-1234567890ab \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --decrypt
```

---

### `snapshot publish` -- Publish a new snapshot

Capture the current resolved configuration for a project as an immutable snapshot.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--project-id` | Guid | Yes | The project ID to publish a snapshot for |
| `--description` | string | No | An optional description for the snapshot |

**Examples:**

```bash
# Publish a snapshot with a description
groundcontrol snapshot publish \
  --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --description "Pre-release configuration freeze for v2.4.0"

# Verify the snapshot was published
groundcontrol snapshot list --project-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

## `audit` -- View audit records

### `audit list` -- List audit records

List audit records with optional filters by entity type or entity ID.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--entity-type` | string | No | Filter by entity type |
| `--entity-id` | Guid | No | Filter by entity ID |

**Examples:**

```bash
# List all audit records for snapshot entities
groundcontrol audit list --entity-type Snapshot

# List audit records for a specific client
groundcontrol audit list \
  --entity-type Client \
  --entity-id b2c3d4e5-f6a7-8901-bcde-f12345678901
```

---

### `audit get` -- Get an audit record by ID

Retrieve the full details of a specific audit record.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The audit record ID |

**Examples:**

```bash
# Get a specific audit record
groundcontrol audit get e5f6a7b8-c9d0-1234-ef01-234567890abc
```
