namespace GroundControl.Cli.Shared.Auth;

internal sealed class TokenResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required int ExpiresIn { get; init; }

    public required int RefreshExpiresIn { get; init; }
}