namespace GroundControl.Cli.Features.Roles.Delete;

internal sealed class DeleteRoleOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}