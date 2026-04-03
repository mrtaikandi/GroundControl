using System.CommandLine;
using GroundControl.Cli.Features.Scopes.Create;
using GroundControl.Cli.Features.Scopes.Delete;
using GroundControl.Cli.Features.Scopes.Get;
using GroundControl.Cli.Features.Scopes.List;
using GroundControl.Cli.Features.Scopes.Update;

namespace GroundControl.Cli.Features.Scopes;

[RootCommand<ScopeDependencyModule>]
internal sealed class ScopeCommand : Command
{
    public ScopeCommand()
        : base("scope", "Manage scopes")
    {
        Subcommands.Add(new ListScopesCommand());
        Subcommands.Add(new GetScopeCommand());
        Subcommands.Add(new CreateScopeCommand());
        Subcommands.Add(new UpdateScopeCommand());
        Subcommands.Add(new DeleteScopeCommand());
    }
}