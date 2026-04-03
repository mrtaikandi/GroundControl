using System.CommandLine;
using GroundControl.Cli.Features.Variables.Create;
using GroundControl.Cli.Features.Variables.Delete;
using GroundControl.Cli.Features.Variables.Get;
using GroundControl.Cli.Features.Variables.List;
using GroundControl.Cli.Features.Variables.Update;

namespace GroundControl.Cli.Features.Variables;

[RootCommand<VariableDependencyModule>]
internal sealed class VariableCommand : Command
{
    public VariableCommand()
        : base("variable", "Manage variables")
    {
        Subcommands.Add(new ListVariablesCommand());
        Subcommands.Add(new GetVariableCommand());
        Subcommands.Add(new CreateVariableCommand());
        Subcommands.Add(new UpdateVariableCommand());
        Subcommands.Add(new DeleteVariableCommand());
    }
}