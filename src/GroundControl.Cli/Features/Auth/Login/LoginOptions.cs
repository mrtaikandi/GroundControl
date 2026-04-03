namespace GroundControl.Cli.Features.Auth.Login;

internal sealed class LoginOptions
{
    public string? ServerUrl { get; set; }

    public AuthMethod? Method { get; set; }

    public string? Token { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}