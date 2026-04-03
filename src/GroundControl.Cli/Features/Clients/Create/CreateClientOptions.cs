namespace GroundControl.Cli.Features.Clients.Create;

internal sealed class CreateClientOptions
{
    public Guid ProjectId { get; set; }

    public string? Name { get; set; }

    public string? Scopes { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}