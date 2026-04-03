namespace GroundControl.Cli.Features.Groups.Update;

internal sealed class UpdateGroupOptions
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public long? Version { get; set; }
}