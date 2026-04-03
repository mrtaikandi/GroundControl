namespace GroundControl.Cli.Features.Users.Delete;

internal sealed class DeleteUserOptions
{
    public Guid Id { get; set; }

    public long? Version { get; set; }

    public bool Yes { get; set; }
}