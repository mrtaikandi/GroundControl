using System.CommandLine;

namespace GroundControl.Cli.Features.Clients.List;

internal sealed class ListClientsCommand : Command<ListClientsHandler, ListClientsOptions>
{
    public ListClientsCommand()
        : base("list", "List all clients for a project")
    {
        var projectIdOption = new Option<Guid>("--project-id", "The project ID");

        Options.Add(projectIdOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ProjectId = parseResult.GetValue(projectIdOption);
        });
    }
}