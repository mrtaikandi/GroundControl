using System.Net;
using System.Net.Http.Headers;
using GroundControl.Cli.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GroundControl.Cli.Tests.Shared.Auth;

public sealed class CredentialAuthenticatingHandlerTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly TokenCache _tokenCache;

    public CredentialAuthenticatingHandlerTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _tokenCache = new TokenCache(_timeProvider);
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_UsesCachedAccessToken()
    {
        // Arrange
        _tokenCache.SetTokens("cached-access-token", "cached-refresh-token", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("cached-access-token");

        // Should NOT have called token client since we had a cached token
        await tokenClient.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_LogsInWhenNoCachedToken()
    {
        // Arrange
        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        tokenClient.LoginAsync("admin", "secret", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                ExpiresIn = 3600,
                RefreshExpiresIn = 86400
            });

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("new-access-token");

        await tokenClient.Received(1).LoginAsync("admin", "secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_RefreshesWhenAccessTokenExpired()
    {
        // Arrange
        _tokenCache.SetTokens("old-access", "valid-refresh-token", expiresInSeconds: 60, refreshExpiresInSeconds: 86400);
        _timeProvider.Advance(TimeSpan.FromSeconds(61)); // expire the access token

        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        tokenClient.RefreshAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse
            {
                AccessToken = "refreshed-access-token",
                RefreshToken = "refreshed-refresh-token",
                ExpiresIn = 3600,
                RefreshExpiresIn = 86400
            });

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("refreshed-access-token");

        await tokenClient.Received(1).RefreshAsync("valid-refresh-token", Arg.Any<CancellationToken>());
        await tokenClient.DidNotReceive().LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_ThrowsWhenBothTokensExpired()
    {
        // Arrange
        _tokenCache.SetTokens("old-access", "old-refresh", expiresInSeconds: 60, refreshExpiresInSeconds: 120);
        _timeProvider.Advance(TimeSpan.FromSeconds(121)); // expire both tokens

        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("re-authenticate");
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_MissingUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Credentials", Username = null, Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Credentials");
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_MissingPassword_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = null };
        var tokenClient = Substitute.For<ITokenClient>();
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Credentials");
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_CachesTokensAfterLogin()
    {
        // Arrange
        var options = new AuthOptions { Method = "Credentials", Username = "admin", Password = "secret" };
        var tokenClient = Substitute.For<ITokenClient>();
        tokenClient.LoginAsync("admin", "secret", Arg.Any<CancellationToken>())
            .Returns(new TokenResponse
            {
                AccessToken = "new-access",
                RefreshToken = "new-refresh",
                ExpiresIn = 3600,
                RefreshExpiresIn = 86400
            });

        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options, tokenClient);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act — first request triggers login
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Act — second request should use cached token
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert — only one login call
        await tokenClient.Received(1).LoginAsync("admin", "secret", Arg.Any<CancellationToken>());
    }

    private AuthenticatingHandler CreateHandler(AuthOptions options, ITokenClient tokenClient) =>
        new(Options.Create(options), _tokenCache, tokenClient);

    public void Dispose() => _tokenCache.Dispose();
}