using System.CommandLine;

namespace GroundControl.Cli.Features.Snapshots.Publish;

internal sealed class PublishSnapshotCommand : Command<PublishSnapshotHandler, PublishSnapshotOptions>
{
    public PublishSnapshotCommand()
        : base("publish", "Publish a new snapshot for a project")
    {
        var projectIdOption = new Option<Guid?>("--project-id", "The project ID to publish a snapshot for");
        var descriptionOption = new Option<string?>("--description", "An optional description for the snapshot");

        Options.Add(projectIdOption);
        Options.Add(descriptionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Description = parseResult.GetValue(descriptionOption);
        });
    }
}