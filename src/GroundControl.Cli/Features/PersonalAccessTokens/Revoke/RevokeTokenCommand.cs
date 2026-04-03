using System.CommandLine;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Revoke;

internal sealed class RevokeTokenCommand : Command<RevokeTokenHandler, RevokeTokenOptions>
{
    public RevokeTokenCommand()
        : base("revoke", "Revoke a personal access token")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The token ID" };
        var yesOption = new Option<bool>("--yes") { Description = "Skip confirmation prompt" };

        Arguments.Add(idArgument);
        Options.Add(yesOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.Yes = parseResult.GetValue(yesOption);
        });
    }
}