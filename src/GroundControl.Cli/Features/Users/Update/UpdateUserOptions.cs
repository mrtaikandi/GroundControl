namespace GroundControl.Cli.Features.Users.Update;

internal sealed class UpdateUserOptions
{
    public Guid Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public bool? IsActive { get; set; }

    public string[]? Grants { get; set; }

    public long? Version { get; set; }
}