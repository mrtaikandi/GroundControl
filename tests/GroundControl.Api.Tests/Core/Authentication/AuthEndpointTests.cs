using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroundControl.Api.Core.Authentication.Contracts;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.Authentication;

public sealed class AuthEndpointTests : ApiHandlerTestBase
{
    public AuthEndpointTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    { }

    [Fact]
    public async Task TokenLogin_WithValidCredentials_ReturnsTokenResponse()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokenResponse = await ReadRequiredJsonAsync<TokenResponse>(response, TestCancellationToken);
        tokenResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokenResponse.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokenResponse.ExpiresIn.ShouldBeGreaterThan(0);
        tokenResponse.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task TokenLogin_WithWrongPassword_Returns401()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = "WrongPassword!" }, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenRefresh_WithValidToken_ReturnsNewTokenPair()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(loginResponse, TestCancellationToken);

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/auth/token/refresh", new { RefreshToken = tokens.RefreshToken }, TestCancellationToken);

        // Assert
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newTokens = await ReadRequiredJsonAsync<TokenResponse>(refreshResponse, TestCancellationToken);
        newTokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        newTokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        newTokens.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task TokenRefresh_WithReusedToken_RevokesEntireFamily()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(loginResponse, TestCancellationToken);

        // Use the refresh token once (legitimate)
        await client.PostAsJsonAsync("/auth/token/refresh", new { RefreshToken = tokens.RefreshToken }, TestCancellationToken);

        // Act — reuse the same refresh token (replay attack)
        var reuseResponse = await client.PostAsJsonAsync("/auth/token/refresh", new { RefreshToken = tokens.RefreshToken }, TestCancellationToken);

        // Assert — reuse detected, entire family revoked
        reuseResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify all tokens in the family are revoked
        var refreshTokensCollection = factory.Database.GetCollection<RefreshToken>("refresh_tokens");
        var allTokens = await refreshTokensCollection.Find(FilterDefinition<RefreshToken>.Empty).ToListAsync(TestCancellationToken);
        allTokens.ShouldNotBeEmpty();
        allTokens.ShouldAllBe(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task TokenRefresh_WithExpiredToken_Returns401()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        var loginResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(loginResponse, TestCancellationToken);

        // Manually expire the token in the database
        var refreshTokensCollection = factory.Database.GetCollection<RefreshToken>("refresh_tokens");
        var update = Builders<RefreshToken>.Update.Set(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-1));
        await refreshTokensCollection.UpdateManyAsync(FilterDefinition<RefreshToken>.Empty, update, cancellationToken: TestCancellationToken);

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/auth/token/refresh", new { RefreshToken = tokens.RefreshToken }, TestCancellationToken);

        // Assert
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CookieLogin_WithValidCredentials_SetsCookieAndReturnsOk()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/auth/login", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeTrue();
        var userResponse = await ReadRequiredJsonAsync<UserResponse>(response, TestCancellationToken);
        userResponse.Username.ShouldBe(SeedUsername);
        userResponse.Email.ShouldBe(SeedEmail);
    }

    [Fact]
    public async Task CookieLogout_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        // Login first to get a cookie
        await client.PostAsJsonAsync("/auth/login", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);

        // Act — get a JWT to call logout (which requires auth)
        var tokenResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(tokenResponse, TestCancellationToken);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(logoutRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidJwt_ReturnsUser()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        var tokenResponse = await client.PostAsJsonAsync("/auth/token", new { Username = SeedUsername, Password = SeedPassword }, TestCancellationToken);
        var tokens = await ReadRequiredJsonAsync<TokenResponse>(tokenResponse, TestCancellationToken);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var userResponse = await ReadRequiredJsonAsync<UserResponse>(response, TestCancellationToken);
        userResponse.Username.ShouldBe(SeedUsername);
        userResponse.Email.ShouldBe(SeedEmail);
        userResponse.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCurrentUser_WithoutCredentials_Returns401()
    {
        // Arrange
        await using var factory = CreateApiFactoryWithBuiltInAuthentication();
        using var client = factory.CreateClient();
        await EnsureHostStartedAsync(client);

        // Act
        var response = await client.GetAsync("/auth/me", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    private static async Task EnsureHostStartedAsync(HttpClient client) =>
        await client.GetAsync("/healthz/liveness");
}