namespace GroundControl.Api.Features.Authentication.Contracts;

internal sealed record TokenResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required int ExpiresIn { get; init; }

    public string TokenType { get; init; } = "Bearer";
}