namespace GroundControl.Cli.Features.Roles;

internal static class KnownPermissions
{
    internal static readonly IReadOnlyList<string> All =
    [
        "scopes:read",
        "scopes:write",
        "groups:read",
        "groups:write",
        "users:read",
        "users:write",
        "roles:read",
        "roles:write",
        "projects:read",
        "projects:write",
        "templates:read",
        "templates:write",
        "variables:read",
        "variables:write",
        "config-entries:read",
        "config-entries:write",
        "snapshots:read",
        "snapshots:publish",
        "clients:read",
        "clients:write",
        "sensitive_values:decrypt",
        "audit:read"
    ];
}