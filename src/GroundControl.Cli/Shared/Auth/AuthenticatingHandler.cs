using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Shared.Auth;

/// <summary>
/// A delegating handler that injects authentication headers into outgoing HTTP requests
/// based on the configured authentication method.
/// </summary>
internal sealed class AuthenticatingHandler : DelegatingHandler
{
    private readonly AuthOptions _options;
    private readonly TokenCache _tokenCache;
    private readonly ITokenClient _tokenClient;

    public AuthenticatingHandler(IOptions<AuthOptions> options, TokenCache tokenCache, ITokenClient tokenClient)
    {
        _options = options.Value;
        _tokenCache = tokenCache;
        _tokenClient = tokenClient;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_options.Method?.ToUpperInvariant())
        {
            case null or "" or "NONE":
                break;

            case "BEARER":
                if (string.IsNullOrWhiteSpace(_options.Token))
                {
                    throw new InvalidOperationException(
                        "Authentication method is set to 'Bearer' but no token is configured. " +
                        "Run 'groundcontrol auth login' to configure your credentials.");
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
                break;

            case "APIKEY":
                if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                {
                    throw new InvalidOperationException(
                        "Authentication method is set to 'ApiKey' but client credentials are incomplete. " +
                        "Run 'groundcontrol auth login' to configure your credentials.");
                }

                // Custom ApiKey scheme with colon in value requires TryAddWithoutValidation
                // to bypass AuthenticationHeaderValue parsing.
                request.Headers.TryAddWithoutValidation("Authorization", $"ApiKey {_options.ClientId}:{_options.ClientSecret}");
                break;

            case "CREDENTIALS":
                var accessToken = await GetCredentialAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown authentication method '{_options.Method}'. " +
                    "Supported methods are: Bearer, ApiKey, Credentials. " +
                    "Run 'groundcontrol auth login' to reconfigure your credentials.");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetCredentialAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Authentication method is set to 'Credentials' but username or password is missing. " +
                "Run 'groundcontrol auth login' to configure your credentials.");
        }

        // Fast path: valid cached access token (lock-free read is safe here because the
        // CLI is single-threaded per command, and the worst case on a racy read is an
        // unnecessary lock acquisition — the double-check inside the lock is authoritative).
        if (_tokenCache.HasValidAccessToken)
        {
            return _tokenCache.AccessToken!;
        }

        // Slow path: acquire lock to refresh/login (prevents concurrent refresh race)
        return await _tokenCache.WithRefreshLockAsync(async () =>
        {
            // Double-check after acquiring lock — another thread may have refreshed
            if (_tokenCache.HasValidAccessToken)
            {
                return _tokenCache.AccessToken!;
            }

            // Try refresh if we have a valid refresh token
            if (_tokenCache.HasValidRefreshToken)
            {
                var refreshResponse = await _tokenClient.RefreshAsync(
                    _tokenCache.RefreshToken!, cancellationToken).ConfigureAwait(false);

                _tokenCache.SetTokens(
                    refreshResponse.AccessToken,
                    refreshResponse.RefreshToken,
                    refreshResponse.ExpiresIn,
                    refreshResponse.RefreshExpiresIn);

                return refreshResponse.AccessToken;
            }

            // If tokens were previously set but both are now expired, require re-authentication
            if (_tokenCache.WasPopulated)
            {
                throw new InvalidOperationException(
                    "Your session has expired. Run 'groundcontrol auth login' to re-authenticate.");
            }

            // First request — no tokens yet, login with stored credentials
            var loginResponse = await _tokenClient.LoginAsync(
                _options.Username!, _options.Password!, cancellationToken).ConfigureAwait(false);

            _tokenCache.SetTokens(
                loginResponse.AccessToken,
                loginResponse.RefreshToken,
                loginResponse.ExpiresIn,
                loginResponse.RefreshExpiresIn);

            return loginResponse.AccessToken;
        }, cancellationToken).ConfigureAwait(false);
    }
}