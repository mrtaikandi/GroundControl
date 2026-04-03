namespace GroundControl.Cli.Features.Clients.Delete;

internal sealed class DeleteClientOptions
{
    public Guid ProjectId { get; set; }

    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}