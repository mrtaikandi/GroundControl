using System.CommandLine;
using GroundControl.Cli.Features.Config.Import;
using GroundControl.Cli.Features.Config.Show;

namespace GroundControl.Cli.Features.Config;

[RootCommand<ConfigDependencyModule>]
internal sealed class ConfigCommand : Command
{
    public ConfigCommand()
        : base("config", "Manage GroundControl configuration")
    {
        Subcommands.Add(new ImportConfigCommand());
        Subcommands.Add(new ShowConfigCommand());
    }
}