using System.CommandLine;

namespace GroundControl.Cli.Features.Scopes.Get;

internal sealed class GetScopeCommand : Command<GetScopeHandler, GetScopeOptions>
{
    public GetScopeCommand()
        : base("get", "Get a scope by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The scope ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}