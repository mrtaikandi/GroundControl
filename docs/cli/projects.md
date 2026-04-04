# Projects and Organization

Commands for managing the organizational hierarchy in GroundControl. Groups serve as top-level containers, projects belong to groups and hold configuration entries, templates provide reusable configuration that projects can inherit, and scopes define the dimensions used to target configuration to specific environments or regions.

---

## `group` -- Manage groups

Groups are top-level organizational containers for projects and templates. Every project and template belongs to a group.

### `group list` -- List all groups

Lists all groups in the system.

**Examples:**

```bash
# List all groups
groundcontrol group list
```

---

### `group get` -- Get a group by ID

Retrieves the details of a single group, including its current version.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the group |

**Examples:**

```bash
# Get a specific group
groundcontrol group get a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `group create` -- Create a new group

Creates a new group that can contain projects and templates.

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | Yes | The group name |
| `--description` | string | No | The group description |

**Examples:**

```bash
# Create a group with a name only
groundcontrol group create --name "Platform Services"

# Create a group with a name and description
groundcontrol group create --name "Platform Services" --description "Backend infrastructure services"
```

---

### `group update` -- Update a group

Updates an existing group. Requires the current version for optimistic concurrency -- retrieve it first with `group get`.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the group |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | No | The new group name |
| `--description` | string | No | The new description |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the current version of the group
groundcontrol group get a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Then update using the version from the response
groundcontrol group update a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --name "Platform Services (Renamed)" \
  --version 3
```

---

### `group delete` -- Delete a group

Deletes a group. The group must not contain any projects or templates. Use `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the group |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete a group with confirmation prompt
groundcontrol group delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --version 3

# Delete a group without confirmation
groundcontrol group delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --version 3 --yes
```

---

## `project` -- Manage projects

Projects belong to groups and contain configuration entries. A project can optionally inherit configuration from one or more templates. Use `group create` to create a group before assigning projects to it, and `template create` to set up templates that projects can reference.

### `project list` -- List all projects

Lists all projects in the system.

**Examples:**

```bash
# List all projects
groundcontrol project list
```

---

### `project get` -- Get a project by ID

Retrieves the details of a single project, including its group assignment, associated templates, and current version.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the project |

**Examples:**

```bash
# Get a specific project
groundcontrol project get b2c3d4e5-f6a7-8901-bcde-f12345678901
```

---

### `project create` -- Create a new project

Creates a new project. Projects can be assigned to a group and can inherit from multiple templates. Template IDs are provided as a comma-separated list.

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | Yes | The project name |
| `--description` | string | No | The project description |
| `--group-id` | Guid | No | The owning group ID (see `group list`) |
| `--template-ids` | string | No | Comma-separated template IDs (see `template list`) |

**Examples:**

```bash
# Create a simple project
groundcontrol project create --name "Payment Gateway"

# Create a project assigned to a group
groundcontrol project create \
  --name "Payment Gateway" \
  --description "Handles payment processing" \
  --group-id a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Create a project with a group and multiple templates
groundcontrol project create \
  --name "Payment Gateway" \
  --description "Handles payment processing" \
  --group-id a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  --template-ids "c3d4e5f6-a7b8-9012-cdef-123456789012,d4e5f6a7-b8c9-0123-defa-234567890123"
```

---

### `project update` -- Update a project

Updates an existing project. Requires the current version for optimistic concurrency -- retrieve it first with `project get`. Providing `--template-ids` replaces the entire list of associated templates.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the project |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | No | The new project name |
| `--description` | string | No | The new description |
| `--group-id` | Guid | No | The new owning group ID |
| `--template-ids` | string | No | Comma-separated template IDs (replaces existing) |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the current version
groundcontrol project get b2c3d4e5-f6a7-8901-bcde-f12345678901

# Update the project name and move it to a different group
groundcontrol project update b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --name "Payment Gateway v2" \
  --group-id e5f6a7b8-c9d0-1234-efab-345678901234 \
  --version 5

# Replace the template list
groundcontrol project update b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  --template-ids "c3d4e5f6-a7b8-9012-cdef-123456789012" \
  --version 6
```

---

### `project delete` -- Delete a project

Deletes a project. The project must not have dependent configuration entries. Use `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the project |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete a project with confirmation
groundcontrol project delete b2c3d4e5-f6a7-8901-bcde-f12345678901 --version 5

# Delete a project without confirmation
groundcontrol project delete b2c3d4e5-f6a7-8901-bcde-f12345678901 --version 5 --yes
```

---

## `template` -- Manage templates

Templates define reusable configuration that can be inherited by one or more projects. Like projects, templates belong to groups. Create templates first, then reference their IDs when creating or updating projects with `--template-ids`.

### `template list` -- List all templates

Lists all templates in the system.

**Examples:**

```bash
# List all templates
groundcontrol template list
```

---

### `template get` -- Get a template by ID

Retrieves the details of a single template, including its group assignment and current version.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the template |

**Examples:**

```bash
# Get a specific template
groundcontrol template get c3d4e5f6-a7b8-9012-cdef-123456789012
```

---

### `template create` -- Create a new template

Creates a new template that can be assigned to a group and later referenced by projects.

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | Yes | The template name |
| `--description` | string | No | The template description |
| `--group-id` | Guid | No | The owning group ID (see `group list`) |

**Examples:**

```bash
# Create a template
groundcontrol template create --name "Microservice Defaults"

# Create a template within a group
groundcontrol template create \
  --name "Microservice Defaults" \
  --description "Standard config for all microservices" \
  --group-id a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

### `template update` -- Update a template

Updates an existing template. Requires the current version for optimistic concurrency -- retrieve it first with `template get`.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the template |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | No | The new template name |
| `--description` | string | No | The new description |
| `--group-id` | Guid | No | The new owning group ID |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the current version
groundcontrol template get c3d4e5f6-a7b8-9012-cdef-123456789012

# Update the template description
groundcontrol template update c3d4e5f6-a7b8-9012-cdef-123456789012 \
  --description "Updated standard config for all microservices" \
  --version 2
```

---

### `template delete` -- Delete a template

Deletes a template. The template must not be referenced by any projects. Use `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the template |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete a template
groundcontrol template delete c3d4e5f6-a7b8-9012-cdef-123456789012 --version 2

# Delete without confirmation
groundcontrol template delete c3d4e5f6-a7b8-9012-cdef-123456789012 --version 2 --yes
```

---

## `scope` -- Manage scopes

Scopes define dimensions and their allowed values for targeting configuration to specific contexts. For example, an "Environment" dimension might allow the values `dev`, `staging`, and `prod`, while a "Region" dimension might allow `us-east`, `us-west`, and `eu-west`. Configuration entries reference these scopes to control which values apply in which contexts.

### `scope list` -- List all scopes

Lists all scope dimensions and their allowed values.

**Examples:**

```bash
# List all scopes
groundcontrol scope list
```

---

### `scope get` -- Get a scope by ID

Retrieves the details of a single scope, including its dimension name, allowed values, and current version.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the scope |

**Examples:**

```bash
# Get a specific scope
groundcontrol scope get f6a7b8c9-d0e1-2345-abcd-567890123456
```

---

### `scope create` -- Create a new scope

Creates a new scope dimension with its allowed values. Dimension names should be descriptive (e.g., `Environment`, `Region`, `Tenant`).

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--dimension` | string | Yes | The scope dimension name (e.g., `Environment`, `Region`) |
| `--values` | string | Yes | Comma-separated allowed values (e.g., `dev,staging,prod`) |
| `--description` | string | No | The scope description |

**Examples:**

```bash
# Create an environment scope
groundcontrol scope create \
  --dimension "Environment" \
  --values "dev,staging,prod" \
  --description "Deployment environment"

# Create a region scope
groundcontrol scope create \
  --dimension "Region" \
  --values "us-east,us-west,eu-west" \
  --description "Cloud deployment region"
```

---

### `scope update` -- Update a scope

Updates an existing scope. Requires the current version for optimistic concurrency -- retrieve it first with `scope get`. Providing `--values` replaces the entire list of allowed values.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the scope |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--dimension` | string | No | The new dimension name |
| `--values` | string | No | Comma-separated allowed values (replaces existing) |
| `--description` | string | No | The new description |
| `--version` | long | No | Expected version for optimistic concurrency |

**Examples:**

```bash
# First, get the current version
groundcontrol scope get f6a7b8c9-d0e1-2345-abcd-567890123456

# Add a new allowed value to the environment scope
groundcontrol scope update f6a7b8c9-d0e1-2345-abcd-567890123456 \
  --values "dev,staging,prod,canary" \
  --version 1
```

---

### `scope delete` -- Delete a scope

Deletes a scope dimension. The scope must not be referenced by any configuration entries. Use `--yes` to skip the confirmation prompt.

**Arguments:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | Guid | Yes | The unique identifier of the scope |

**Options:**

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--version` | long | No | Expected version for optimistic concurrency |
| `--yes` | flag | No | Skip confirmation prompt |

**Examples:**

```bash
# Delete a scope
groundcontrol scope delete f6a7b8c9-d0e1-2345-abcd-567890123456 --version 1

# Delete without confirmation
groundcontrol scope delete f6a7b8c9-d0e1-2345-abcd-567890123456 --version 1 --yes
```
