using System.CommandLine;
using GroundControl.Cli.Features.Roles.Create;
using GroundControl.Cli.Features.Roles.Delete;
using GroundControl.Cli.Features.Roles.Get;
using GroundControl.Cli.Features.Roles.List;
using GroundControl.Cli.Features.Roles.Update;

namespace GroundControl.Cli.Features.Roles;

[RootCommand<RoleDependencyModule>]
internal sealed class RoleCommand : Command
{
    public RoleCommand()
        : base("role", "Manage roles")
    {
        Subcommands.Add(new ListRolesCommand());
        Subcommands.Add(new GetRoleCommand());
        Subcommands.Add(new CreateRoleCommand());
        Subcommands.Add(new UpdateRoleCommand());
        Subcommands.Add(new DeleteRoleCommand());
    }
}