namespace GroundControl.Cli.Features.Roles.List;

internal sealed class ListRolesCommand : Command<ListRolesHandler, ListRolesOptions>
{
    public ListRolesCommand()
        : base("list", "List all roles")
    {
    }
}