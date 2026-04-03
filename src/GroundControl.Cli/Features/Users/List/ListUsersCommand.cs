namespace GroundControl.Cli.Features.Users.List;

internal sealed class ListUsersCommand : Command<ListUsersHandler, ListUsersOptions>
{
    public ListUsersCommand()
        : base("list", "List all users")
    {
    }
}