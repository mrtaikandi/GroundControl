using System.CommandLine;

namespace GroundControl.Cli.Features.Roles.Get;

internal sealed class GetRoleCommand : Command<GetRoleHandler, GetRoleOptions>
{
    public GetRoleCommand()
        : base("get", "Get a role by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The role ID" };
        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}