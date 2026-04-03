using System.CommandLine;
using GroundControl.Cli.Features.Snapshots.Get;
using GroundControl.Cli.Features.Snapshots.List;
using GroundControl.Cli.Features.Snapshots.Publish;

namespace GroundControl.Cli.Features.Snapshots;

[RootCommand<SnapshotDependencyModule>]
internal sealed class SnapshotCommand : Command
{
    public SnapshotCommand()
        : base("snapshot", "Manage snapshots")
    {
        Subcommands.Add(new ListSnapshotsCommand());
        Subcommands.Add(new GetSnapshotCommand());
        Subcommands.Add(new PublishSnapshotCommand());
    }
}