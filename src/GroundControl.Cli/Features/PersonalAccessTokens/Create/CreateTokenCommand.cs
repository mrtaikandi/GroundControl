using System.CommandLine;

namespace GroundControl.Cli.Features.PersonalAccessTokens.Create;

internal sealed class CreateTokenCommand : Command<CreateTokenHandler, CreateTokenOptions>
{
    public CreateTokenCommand()
        : base("create", "Create a new personal access token")
    {
        var nameOption = new Option<string?>("--name", "The token name");
        var expiresInOption = new Option<string?>("--expires-in", "Token lifetime (e.g. 30d, 6m, 1y). Defaults to 30 days if omitted.");

        Options.Add(nameOption);
        Options.Add(expiresInOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Name = parseResult.GetValue(nameOption);
            options.ExpiresIn = parseResult.GetValue(expiresInOption);
        });
    }
}