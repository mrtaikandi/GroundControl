using System.CommandLine;

namespace GroundControl.Cli.Features.Clients.Get;

internal sealed class GetClientCommand : Command<GetClientHandler, GetClientOptions>
{
    public GetClientCommand()
        : base("get", "Get a client by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The client ID" };
        var projectIdOption = new Option<Guid>("--project-id", "The project ID");

        Arguments.Add(idArgument);
        Options.Add(projectIdOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.ProjectId = parseResult.GetValue(projectIdOption);
        });
    }
}