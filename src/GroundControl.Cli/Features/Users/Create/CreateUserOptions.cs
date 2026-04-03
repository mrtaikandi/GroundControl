namespace GroundControl.Cli.Features.Users.Create;

internal sealed class CreateUserOptions
{
    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string[]? Grants { get; set; }
}