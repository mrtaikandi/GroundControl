namespace GroundControl.Cli.Features.Groups.List;

internal sealed class ListGroupsCommand : Command<ListGroupsHandler, ListGroupsOptions>
{
    public ListGroupsCommand()
        : base("list", "List all groups")
    {
    }
}