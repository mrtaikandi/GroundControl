using System.Net.Http.Json;
using System.Text.Json;

namespace GroundControl.Cli.Shared.Auth;

internal sealed class TokenClient : ITokenClient
{
    private static readonly Uri TokenEndpoint = new("/auth/token", UriKind.Relative);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;

    public TokenClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        });

        return await PostTokenRequestAsync(content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        return await PostTokenRequestAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenResponse> PostTokenRequestAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenEndpointResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return tokenResponse is null
            ? throw new InvalidOperationException("Token endpoint returned an empty response.")
            : new TokenResponse
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresIn = tokenResponse.ExpiresIn,
                RefreshExpiresIn = tokenResponse.RefreshExpiresIn
            };
    }

    private sealed class TokenEndpointResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public int ExpiresIn { get; set; }

        public int RefreshExpiresIn { get; set; }
    }
}