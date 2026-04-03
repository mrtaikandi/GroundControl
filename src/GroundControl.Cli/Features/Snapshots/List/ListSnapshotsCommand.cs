using System.CommandLine;

namespace GroundControl.Cli.Features.Snapshots.List;

internal sealed class ListSnapshotsCommand : Command<ListSnapshotsHandler, ListSnapshotsOptions>
{
    public ListSnapshotsCommand()
        : base("list", "List snapshots for a project")
    {
        var projectIdOption = new Option<Guid?>("--project-id") { Description = "The project ID to list snapshots for" };

        Options.Add(projectIdOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ProjectId = parseResult.GetValue(projectIdOption);
        });
    }
}