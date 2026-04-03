namespace GroundControl.Cli.Features.Projects.Delete;

internal sealed class DeleteProjectOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}