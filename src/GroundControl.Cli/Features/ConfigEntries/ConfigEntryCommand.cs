using System.CommandLine;
using GroundControl.Cli.Features.ConfigEntries.Create;
using GroundControl.Cli.Features.ConfigEntries.Delete;
using GroundControl.Cli.Features.ConfigEntries.Get;
using GroundControl.Cli.Features.ConfigEntries.List;
using GroundControl.Cli.Features.ConfigEntries.Update;

namespace GroundControl.Cli.Features.ConfigEntries;

[RootCommand<ConfigEntryDependencyModule>]
internal sealed class ConfigEntryCommand : Command
{
    public ConfigEntryCommand()
        : base("config-entry", "Manage configuration entries")
    {
        Subcommands.Add(new ListConfigEntriesCommand());
        Subcommands.Add(new GetConfigEntryCommand());
        Subcommands.Add(new CreateConfigEntryCommand());
        Subcommands.Add(new UpdateConfigEntryCommand());
        Subcommands.Add(new DeleteConfigEntryCommand());
    }
}