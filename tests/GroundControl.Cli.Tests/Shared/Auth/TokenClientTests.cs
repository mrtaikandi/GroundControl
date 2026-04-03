using System.Net;
using System.Text.Json;
using GroundControl.Cli.Shared.Auth;

namespace GroundControl.Cli.Tests.Shared.Auth;

public sealed class TokenClientTests
{
    private const string TokenEndpoint = "/auth/token";

    [Fact]
    public async Task LoginAsync_SendsCredentialsAndReturnsTokens()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            access_token = "access-123",
            refresh_token = "refresh-456",
            expires_in = 3600,
            refresh_expires_in = 86400
        });

        using var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Post, TokenEndpoint, HttpStatusCode.OK, responseJson);

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        innerHandler.OnSend = req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        };

        using var httpClient = new HttpClient(innerHandler, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };
        var tokenClient = new TokenClient(httpClient);

        // Act
        var result = await tokenClient.LoginAsync("admin", "secret", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-123");
        result.RefreshToken.ShouldBe("refresh-456");
        result.ExpiresIn.ShouldBe(3600);
        result.RefreshExpiresIn.ShouldBe(86400);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("admin");
        capturedBody.ShouldContain("secret");
        capturedBody.ShouldContain("grant_type");
    }

    [Fact]
    public async Task RefreshAsync_SendsRefreshTokenAndReturnsNewTokens()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            access_token = "new-access-789",
            refresh_token = "new-refresh-012",
            expires_in = 3600,
            refresh_expires_in = 86400
        });

        using var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Post, TokenEndpoint, HttpStatusCode.OK, responseJson);

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        innerHandler.OnSend = req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        };

        using var httpClient = new HttpClient(innerHandler, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };
        var tokenClient = new TokenClient(httpClient);

        // Act
        var result = await tokenClient.RefreshAsync("old-refresh-token", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("new-access-789");
        result.RefreshToken.ShouldBe("new-refresh-012");

        capturedRequest.ShouldNotBeNull();
        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("old-refresh-token");
        capturedBody.ShouldContain("grant_type");
    }

    [Fact]
    public async Task LoginAsync_WhenServerReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        using var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Post, TokenEndpoint, HttpStatusCode.Unauthorized);

        using var httpClient = new HttpClient(innerHandler, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };
        var tokenClient = new TokenClient(httpClient);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            tokenClient.LoginAsync("admin", "wrong-password", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefreshAsync_WhenServerReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        using var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Post, TokenEndpoint, HttpStatusCode.Unauthorized);

        using var httpClient = new HttpClient(innerHandler, disposeHandler: false) { BaseAddress = new Uri("https://localhost") };
        var tokenClient = new TokenClient(httpClient);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            tokenClient.RefreshAsync("expired-refresh-token", TestContext.Current.CancellationToken));
    }
}