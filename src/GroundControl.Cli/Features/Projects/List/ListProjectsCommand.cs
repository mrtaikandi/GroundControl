namespace GroundControl.Cli.Features.Projects.List;

internal sealed class ListProjectsCommand : Command<ListProjectsHandler, ListProjectsOptions>
{
    public ListProjectsCommand()
        : base("list", "List all projects")
    {
    }
}