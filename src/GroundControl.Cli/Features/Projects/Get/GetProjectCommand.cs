using System.CommandLine;

namespace GroundControl.Cli.Features.Projects.Get;

internal sealed class GetProjectCommand : Command<GetProjectHandler, GetProjectOptions>
{
    public GetProjectCommand()
        : base("get", "Get a project by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The project ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}