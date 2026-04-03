namespace GroundControl.Cli.Features.Clients.Update;

internal sealed class UpdateClientOptions
{
    public Guid ProjectId { get; set; }

    public Guid Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public long? Version { get; set; }
}