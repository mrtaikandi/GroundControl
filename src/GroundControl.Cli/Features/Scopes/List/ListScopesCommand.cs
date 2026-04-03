namespace GroundControl.Cli.Features.Scopes.List;

internal sealed class ListScopesCommand : Command<ListScopesHandler, ListScopesOptions>
{
    public ListScopesCommand()
        : base("list", "List all scopes")
    {
    }
}