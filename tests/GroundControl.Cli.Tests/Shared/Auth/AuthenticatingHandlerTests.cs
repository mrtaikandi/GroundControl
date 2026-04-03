using System.Net;
using GroundControl.Cli.Shared.Auth;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Tests.Shared.Auth;

public sealed class AuthenticatingHandlerTests
{
    [Fact]
    public async Task SendAsync_NoneMethod_DoesNotAddAuthHeader()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(new AuthOptions());
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_NullMethod_DoesNotAddAuthHeader()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(new AuthOptions { Method = null });
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_EmptyMethod_DoesNotAddAuthHeader()
    {
        // Arrange
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(new AuthOptions { Method = "" });
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_BearerMethod_AddsBearerAuthHeader()
    {
        // Arrange
        var options = new AuthOptions { Method = "Bearer", Token = "gc_pat_test123" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.ShouldNotBeNull();
        capturedRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        capturedRequest.Headers.Authorization.Parameter.ShouldBe("gc_pat_test123");
    }

    [Fact]
    public async Task SendAsync_BearerMethod_WhitespaceToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Bearer", Token = "   " };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Bearer");
        exception.Message.ShouldContain("token");
    }

    [Fact]
    public async Task SendAsync_BearerMethod_MissingToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Bearer", Token = null };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Bearer");
        exception.Message.ShouldContain("token");
    }

    [Fact]
    public async Task SendAsync_ApiKeyMethod_AddsApiKeyAuthHeader()
    {
        // Arrange
        var options = new AuthOptions { Method = "ApiKey", ClientId = "my-client-id", ClientSecret = "my-secret" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        HttpRequestMessage? capturedRequest = null;
        innerHandler.OnSend = req => capturedRequest = req;

        // Act
        await client.GetAsync("/api/test", TestContext.Current.CancellationToken);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.TryGetValues("Authorization", out var values).ShouldBeTrue();
        values.ShouldHaveSingleItem().ShouldBe("ApiKey my-client-id:my-secret");
    }

    [Fact]
    public async Task SendAsync_ApiKeyMethod_MissingClientId_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "ApiKey", ClientId = null, ClientSecret = "secret" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("ApiKey");
    }

    [Fact]
    public async Task SendAsync_ApiKeyMethod_WhitespaceClientId_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "ApiKey", ClientId = "  ", ClientSecret = "secret" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("ApiKey");
    }

    [Fact]
    public async Task SendAsync_ApiKeyMethod_MissingClientSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "ApiKey", ClientId = "id", ClientSecret = null };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("ApiKey");
    }

    [Fact]
    public async Task SendAsync_CredentialsMethod_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Credentials", Username = "user", Password = "pass" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<NotSupportedException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Credentials");
    }

    [Fact]
    public async Task SendAsync_UnknownMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthOptions { Method = "Unknown" };
        var innerHandler = new FakeHttpHandler()
            .RespondTo(HttpMethod.Get, "/api/test", HttpStatusCode.OK);

        using var handler = CreateHandler(options);
        handler.InnerHandler = innerHandler;

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri("https://localhost");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            client.GetAsync("/api/test", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Unknown");
    }

    private static AuthenticatingHandler CreateHandler(AuthOptions options) =>
        new(Options.Create(options));
}