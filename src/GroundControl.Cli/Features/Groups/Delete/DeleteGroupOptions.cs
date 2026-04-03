namespace GroundControl.Cli.Features.Groups.Delete;

internal sealed class DeleteGroupOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}