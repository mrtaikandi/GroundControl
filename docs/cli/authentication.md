# Authentication and User Management

Commands for managing server credentials, users, roles, and personal access tokens. These commands handle the full lifecycle of authentication -- from logging in to a GroundControl server to managing the users, roles, and tokens that control access.

---

## `auth` -- Manage server credentials

### `auth login` -- Log in to a GroundControl server

Authenticates the CLI against a GroundControl server and stores the credentials locally. The authentication method determines which additional options are required.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--server-url` | string | Yes | The server URL |
| `--method` | AuthMethod | Yes | Authentication method: `None`, `Pat`, `ApiKey`, `Credentials` |
| `--token` | string | No | Personal access token (used with `Pat` method) |
| `--client-id` | string | No | API key client ID (used with `ApiKey` method) |
| `--client-secret` | string | No | API key client secret (used with `ApiKey` method) |
| `--username` | string | No | Username (used with `Credentials` method) |
| `--password` | string | No | Password (used with `Credentials` method) |

**Examples:**

```bash
# Log in using a personal access token
groundcontrol auth login --server-url https://gc.example.com --method Pat --token gc_pat_abc123def456

# Log in using an API key
groundcontrol auth login --server-url https://gc.example.com --method ApiKey --client-id my-service --client-secret s3cret!

# Log in using username and password
groundcontrol auth login --server-url https://gc.example.com --method Credentials --username admin --password p@ssw0rd
```

---

### `auth logout` -- Clear stored credentials

Removes all locally stored credentials for the current server context.

**Examples:**

```bash
groundcontrol auth logout
```

---

### `auth status` -- Show current authentication status

Displays the current authentication state, including the server URL, authentication method, and whether the stored credentials are still valid.

**Examples:**

```bash
groundcontrol auth status
```

---

## `user` -- Manage users

### `user list` -- List all users

Retrieves and displays all users registered on the server.

**Examples:**

```bash
groundcontrol user list
```

---

### `user get` -- Get a user by ID

Retrieves a single user by their unique identifier.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The user ID |

**Examples:**

```bash
groundcontrol user get a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `user create` -- Create a new user

Creates a new user account. When `--password` is omitted in an interactive terminal, the CLI prompts for it securely. One or more roles can be granted at creation time by repeating the `--grant` option.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--username` | string | Yes | The username |
| `--email` | string | Yes | The email address |
| `--password` | string | No | The password (prompted securely in interactive mode) |
| `--grant` | string[] | No | Role ID to grant (can be specified multiple times) |

**Examples:**

```bash
# Create a user and grant multiple roles
groundcontrol user create \
  --username jdoe \
  --email jdoe@example.com \
  --grant b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --grant c3d4e5f6-a7b8-9012-cdef-123456789012
```

---

### `user update` -- Update a user

Updates an existing user. Supply the `--version` option for optimistic concurrency control. Use `user get` first to retrieve the current version number.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The user ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--username` | string | No | The new username |
| `--email` | string | No | The new email |
| `--active` | bool | No | Whether the user is active |
| `--grant` | string[] | No | Role ID to grant (can be specified multiple times) |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the user to find the current version
groundcontrol user get a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Then update using the version from the response
groundcontrol user update a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --username jdoe-updated \
  --email jdoe-new@example.com \
  --active true \
  --version 3
```

---

### `user delete` -- Delete a user

Deletes a user by ID. In interactive mode, a confirmation prompt is shown unless `--yes` is provided.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The user ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete with confirmation prompt
groundcontrol user delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --version 3

# Delete without confirmation
groundcontrol user delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --version 3 --yes
```

---

## `role` -- Manage roles

### `role list` -- List all roles

Retrieves and displays all roles defined on the server.

**Examples:**

```bash
groundcontrol role list
```

---

### `role get` -- Get a role by ID

Retrieves a single role by its unique identifier.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The role ID |

**Examples:**

```bash
groundcontrol role get d4e5f6a7-b8c9-0123-defa-234567890123
```

---

### `role create` -- Create a new role

Creates a new role with a name, optional description, and optional permissions.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--name` | string | Yes | The role name |
| `--permissions` | string | No | Comma-separated permission strings |
| `--description` | string | No | The role description |

**Examples:**

```bash
# Create a role with permissions
groundcontrol role create \
  --name "Config Editor" \
  --permissions "config:read,config:write,scope:read" \
  --description "Can read and write configuration entries and read scopes"
```

---

### `role update` -- Update a role

Updates an existing role. Use `role get` first to retrieve the current version number for optimistic concurrency.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The role ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--name` | string | No | The new role name |
| `--permissions` | string | No | Comma-separated permissions |
| `--description` | string | No | The new description |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# Get the role to find the current version
groundcontrol role get d4e5f6a7-b8c9-0123-defa-234567890123

# Update the role with the version from the response
groundcontrol role update d4e5f6a7-b8c9-0123-defa-234567890123 \
  --name "Config Admin" \
  --permissions "config:read,config:write,config:delete,scope:read,scope:write" \
  --version 2
```

---

### `role delete` -- Delete a role

Deletes a role by ID. In interactive mode, a confirmation prompt is shown unless `--yes` is provided.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The role ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete with confirmation prompt
groundcontrol role delete d4e5f6a7-b8c9-0123-defa-234567890123 --version 2

# Delete without confirmation
groundcontrol role delete d4e5f6a7-b8c9-0123-defa-234567890123 --version 2 --yes
```

---

## `token` -- Manage personal access tokens

### `token list` -- List personal access tokens

Retrieves and displays all personal access tokens for the authenticated user.

**Examples:**

```bash
groundcontrol token list
```

---

### `token get` -- Get a personal access token by ID

Retrieves a single personal access token by its unique identifier.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The token ID |

**Examples:**

```bash
groundcontrol token get e5f6a7b8-c9d0-1234-efab-345678901234
```

---

### `token create` -- Create a new personal access token

Creates a new personal access token. The token value is displayed once upon creation and cannot be retrieved again. If `--expires-in` is omitted, the token defaults to a 30-day lifetime.

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--name` | string | Yes | The token name |
| `--expires-in` | string | No | Token lifetime (e.g., `30d`, `6m`, `1y`). Defaults to 30 days |

**Examples:**

```bash
# Create a token with the default 30-day expiry
groundcontrol token create --name "CI Pipeline"

# Create a token with a custom 6-month expiry
groundcontrol token create --name "Deploy Service" --expires-in 6m

# Create a token with a 1-year expiry
groundcontrol token create --name "Long-lived Integration" --expires-in 1y
```

---

### `token revoke` -- Revoke a personal access token

Revokes a personal access token, immediately invalidating it. In interactive mode, a confirmation prompt is shown unless `--yes` is provided.

**Arguments:**

| Name | Type | Required | Description |
|---|---|---|---|
| `id` | Guid | Yes | The token ID |

**Options:**

| Option | Type | Required | Description |
|---|---|---|---|
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Revoke with confirmation prompt
groundcontrol token revoke e5f6a7b8-c9d0-1234-efab-345678901234

# Revoke without confirmation
groundcontrol token revoke e5f6a7b8-c9d0-1234-efab-345678901234 --yes
```
