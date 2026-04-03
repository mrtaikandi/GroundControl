namespace GroundControl.Cli.Features.Snapshots.Publish;

internal sealed class PublishSnapshotOptions
{
    public Guid? ProjectId { get; set; }

    public string? Description { get; set; }
}