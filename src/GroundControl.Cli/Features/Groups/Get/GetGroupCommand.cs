using System.CommandLine;

namespace GroundControl.Cli.Features.Groups.Get;

internal sealed class GetGroupCommand : Command<GetGroupHandler, GetGroupOptions>
{
    public GetGroupCommand()
        : base("get", "Get a group by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The group ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}