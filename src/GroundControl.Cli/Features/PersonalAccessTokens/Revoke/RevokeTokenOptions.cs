namespace GroundControl.Cli.Features.PersonalAccessTokens.Revoke;

internal sealed class RevokeTokenOptions
{
    public Guid Id { get; set; }

    public bool Yes { get; set; }
}