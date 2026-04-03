namespace GroundControl.Cli.Features.Roles.Update;

internal sealed class UpdateRoleOptions
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Permissions { get; set; }

    public string? Description { get; set; }

    public long? Version { get; set; }
}