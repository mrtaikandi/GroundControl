using System.CommandLine;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Get;

internal sealed class GetTokenCommand : Command<GetTokenHandler, GetTokenOptions>
{
    public GetTokenCommand()
        : base("get", "Get a personal access token by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The token ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}