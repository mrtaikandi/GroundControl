using System.CommandLine;

namespace GroundControl.Cli.Features.PersonalAccessTokens.List;

internal sealed class ListTokensCommand : Command<ListTokensHandler, ListTokensOptions>
{
    public ListTokensCommand()
        : base("list", "List personal access tokens")
    {
    }
}