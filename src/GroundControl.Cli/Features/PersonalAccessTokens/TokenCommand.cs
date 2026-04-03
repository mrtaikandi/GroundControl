using System.CommandLine;
using GroundControl.Cli.Features.PersonalAccessTokens.Create;
using GroundControl.Cli.Features.PersonalAccessTokens.Get;
using GroundControl.Cli.Features.PersonalAccessTokens.List;
using GroundControl.Cli.Features.PersonalAccessTokens.Revoke;

namespace GroundControl.Cli.Features.PersonalAccessTokens;

[RootCommand<TokenDependencyModule>]
internal sealed class TokenCommand : Command
{
    public TokenCommand()
        : base("token", "Manage personal access tokens")
    {
        Subcommands.Add(new ListTokensCommand());
        Subcommands.Add(new GetTokenCommand());
        Subcommands.Add(new CreateTokenCommand());
        Subcommands.Add(new RevokeTokenCommand());
    }
}