namespace GroundControl.Cli.Shared.Auth;

internal interface ITokenClient
{
    Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}