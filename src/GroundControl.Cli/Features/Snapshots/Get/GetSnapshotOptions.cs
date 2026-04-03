namespace GroundControl.Cli.Features.Snapshots.Get;

internal sealed class GetSnapshotOptions
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public bool? Decrypt { get; set; }
}