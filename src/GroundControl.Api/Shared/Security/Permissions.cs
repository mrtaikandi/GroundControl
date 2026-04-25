// ReSharper disable MemberCanBePrivate.Global

namespace GroundControl.Api.Shared.Security;

/// <summary>
/// Defines all permission constants used for authorization policies.
/// </summary>
internal static class Permissions
{
    public const string AuditRead = "audit:read";
    public const string ClientsRead = "clients:read";
    public const string ClientsWrite = "clients:write";
    public const string ConfigEntriesRead = "config-entries:read";
    public const string ConfigEntriesWrite = "config-entries:write";
    public const string GroupsRead = "groups:read";
    public const string GroupsWrite = "groups:write";
    public const string ProjectsRead = "projects:read";
    public const string ProjectsWrite = "projects:write";
    public const string RolesRead = "roles:read";
    public const string RolesWrite = "roles:write";
    public const string ScopesRead = "scopes:read";
    public const string ScopesWrite = "scopes:write";
    public const string SensitiveValuesDecrypt = "sensitive_values:decrypt";
    public const string SnapshotsPublish = "snapshots:publish";
    public const string SnapshotsRead = "snapshots:read";
    public const string TemplatesRead = "templates:read";
    public const string TemplatesWrite = "templates:write";
    public const string UsersRead = "users:read";
    public const string UsersWrite = "users:write";
    public const string VariablesRead = "variables:read";
    public const string VariablesWrite = "variables:write";

    /// <summary>
    /// Gets all defined permissions.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ScopesRead,
        ScopesWrite,
        GroupsRead,
        GroupsWrite,
        UsersRead,
        UsersWrite,
        RolesRead,
        RolesWrite,
        ProjectsRead,
        ProjectsWrite,
        TemplatesRead,
        TemplatesWrite,
        VariablesRead,
        VariablesWrite,
        ConfigEntriesRead,
        ConfigEntriesWrite,
        SnapshotsRead,
        SnapshotsPublish,
        ClientsRead,
        ClientsWrite,
        SensitiveValuesDecrypt,
        AuditRead
    };
}