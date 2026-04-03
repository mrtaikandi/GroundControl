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

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _clientName;

    public TokenClient(IHttpClientFactory httpClientFactory, string clientName)
    {
        _httpClientFactory = httpClientFactory;
        _clientName = clientName;
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
        using var httpClient = _httpClientFactory.CreateClient(_clientName);
        using var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Token endpoint returned an empty response.");
    }
}