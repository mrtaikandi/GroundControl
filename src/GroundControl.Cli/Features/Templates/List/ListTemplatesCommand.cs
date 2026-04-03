namespace GroundControl.Cli.Features.Templates.List;

internal sealed class ListTemplatesCommand : Command<ListTemplatesHandler, ListTemplatesOptions>
{
    public ListTemplatesCommand()
        : base("list", "List all templates")
    {
    }
}