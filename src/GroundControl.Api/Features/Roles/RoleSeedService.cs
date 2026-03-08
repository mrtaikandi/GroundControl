using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Roles;

internal sealed class RoleSeedService : IHostedService
{
    private readonly IRoleStore _roleStore;

    public RoleSeedService(IRoleStore roleStore)
    {
        _roleStore = roleStore ?? throw new ArgumentNullException(nameof(roleStore));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var defaultRole in DefaultRoles)
        {
            var existing = await _roleStore.GetByNameAsync(defaultRole.Name, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                continue;
            }

            var timestamp = DateTimeOffset.UtcNow;
            var role = new Role
            {
                Id = Guid.CreateVersion7(),
                Name = defaultRole.Name,
                Description = defaultRole.Description,
                Permissions = [.. defaultRole.Permissions],
                Version = 1,
                CreatedAt = timestamp,
                CreatedBy = Guid.Empty,
                UpdatedAt = timestamp,
                UpdatedBy = Guid.Empty,
            };

            await _roleStore.CreateAsync(role, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static readonly IReadOnlyList<DefaultRoleDefinition> DefaultRoles =
    [
        new("Viewer", "Read-only access to configuration data.",
        [
            Permissions.ScopesRead,
            Permissions.GroupsRead,
            Permissions.ProjectsRead,
            Permissions.TemplatesRead,
            Permissions.VariablesRead,
            Permissions.ConfigEntriesRead,
            Permissions.SnapshotsRead,
            Permissions.AuditRead,
        ]),

        new("Editor", "Can read and write configuration data.",
        [
            Permissions.ScopesRead,
            Permissions.GroupsRead,
            Permissions.ProjectsRead,
            Permissions.ProjectsWrite,
            Permissions.TemplatesRead,
            Permissions.TemplatesWrite,
            Permissions.VariablesRead,
            Permissions.VariablesWrite,
            Permissions.ConfigEntriesRead,
            Permissions.ConfigEntriesWrite,
            Permissions.SnapshotsRead,
            Permissions.ClientsRead,
            Permissions.ClientsWrite,
            Permissions.AuditRead,
        ]),

        new("Publisher", "Can publish snapshots in addition to editing configuration.",
        [
            Permissions.ScopesRead,
            Permissions.GroupsRead,
            Permissions.ProjectsRead,
            Permissions.ProjectsWrite,
            Permissions.TemplatesRead,
            Permissions.TemplatesWrite,
            Permissions.VariablesRead,
            Permissions.VariablesWrite,
            Permissions.ConfigEntriesRead,
            Permissions.ConfigEntriesWrite,
            Permissions.SnapshotsRead,
            Permissions.SnapshotsPublish,
            Permissions.ClientsRead,
            Permissions.ClientsWrite,
            Permissions.AuditRead,
        ]),

        new("Admin", "Full administrative access to all features.",
            [.. Permissions.All]),
    ];

    private sealed record DefaultRoleDefinition(string Name, string Description, string[] Permissions);
}