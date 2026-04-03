using System.CommandLine;

namespace GroundControl.Cli.Features.Clients.Delete;

internal sealed class DeleteClientCommand : Command<DeleteClientHandler, DeleteClientOptions>
{
    public DeleteClientCommand()
        : base("delete", "Delete a client")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The client ID" };
        var projectIdOption = new Option<Guid>("--project-id", "The project ID");
        var versionOption = new Option<long?>("--version", "The expected version for optimistic concurrency");
        var yesOption = new Option<bool>("--yes", "Skip confirmation prompt");

        Arguments.Add(idArgument);
        Options.Add(projectIdOption);
        Options.Add(versionOption);
        Options.Add(yesOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Version = parseResult.GetValue(versionOption);
            options.Yes = parseResult.GetValue(yesOption);
        });
    }
}