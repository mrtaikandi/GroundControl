namespace GroundControl.Cli.Features.Roles.Create;

internal sealed class CreateRoleOptions
{
    public string? Name { get; set; }

    public string? Permissions { get; set; }

    public string? Description { get; set; }
}