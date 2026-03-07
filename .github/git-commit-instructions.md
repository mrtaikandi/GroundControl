# Git Commit Instructions

## Commit Message Format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

## Commit Types

- **feat**: New feature (MINOR version bump)
- **fix**: Bug fix (PATCH version bump)
- **refactor**: Code restructuring without functional changes
- **perf**: Performance improvements
- **test**: Add or update tests
- **docs**: Documentation changes
- **style**: Code formatting, naming conventions
- **build**: Build system, dependencies, NuGet packages
- **ci**: CI/CD pipeline changes
- **chore**: Maintenance tasks
- **revert**: Reverts a previous commit

## Scope Mapping for .NET Projects

### Convention

Derive the scope by the following convention, but if a scope mapping table is provided, it takes precedence:

1. Use the **last segment** of the project name, lowercased (e.g., `MyProject.Contracts` -> `contracts`)
2. If the last segment **conflicts** with another project, use a short abbreviation (e.g., `Authentication.Api` -> `auth-api`)
3. Test projects share the scope of the project they test (e.g., `MyProject.Contracts.Tests` -> `contracts`)
4. Directories starting with `.` (e.g., `.github`, `.claude`) do **not** require a scope, except if specifically mapped in the scope mapping table.

### Scope Mapping Table

| Project/Directory | Scope | Example |
|-------------------|-------|---------|
|                   |       |         |

## Description

- Use imperative, present tense: "add" not "added" nor "adds"
- Do not capitalize the first letter
- Do not end with a period
- Aim for 50 characters or less, 72 characters maximum

## Body

- Provide for non-trivial changes, separated by a blank line after the description
- Explain **why** the change was made, not how
- Wrap lines at 72 characters

## Footers

Use git trailer format, one blank line after the body:

- `BREAKING CHANGE: <description>` — breaking changes
- `Refs: #<issue>` — reference issues/tickets
- `Closes: #<issue>` — auto-close linked issues
- `Co-authored-by: <name> <email>` — credit co-authors

## Breaking Changes

Indicate with `!` after the type/scope, a `BREAKING CHANGE:` footer, or both:

```
feat(api)!: change authentication response format

BREAKING CHANGE: response now returns nested object instead of flat structure
```

Breaking changes can be part of commits of ANY type.

## Revert Commits

Use `revert:` with the original commit header. The body MUST include `This reverts commit <SHA>.` and should explain why:

```
revert: feat(api): add real-time notification system

This reverts commit 1a2b3c4d5e6f7g8h9i0j.

Caused performance degradation under high load.
```

## Best Practices

- Keep commits focused on a single logical change
- Do not mix different types of changes in one commit
- Do not write vague messages like "fix bug" or "update code"