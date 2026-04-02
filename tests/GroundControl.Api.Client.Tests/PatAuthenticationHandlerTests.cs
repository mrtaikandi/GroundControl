using System.Net;
using GroundControl.Api.Client.Handlers;

namespace GroundControl.Api.Client.Tests;

public sealed class PatAuthenticationHandlerTests
{
    [Fact]
    public void Constructor_NullToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PatAuthenticationHandler(null!));
    }

    [Fact]
    public void Constructor_EmptyToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationHandler(string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationHandler("   "));
    }

    [Fact]
    public void Constructor_MissingPrefix_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => new PatAuthenticationHandler("some_token_value"));
        exception.Message.ShouldContain("gc_pat_");
    }

    [Fact]
    public void Constructor_WrongCasePrefix_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationHandler("GC_PAT_some_token"));
    }

    [Fact]
    public void Constructor_ValidToken_DoesNotThrow()
    {
        // Arrange
        var token = "gc_pat_abc123";

        // Act & Assert
        Should.NotThrow(() => new PatAuthenticationHandler(token));
    }

    [Fact]
    public void Constructor_PrefixOnly_DoesNotThrow()
    {
        // Arrange
        var token = "gc_pat_";

        // Act & Assert
        Should.NotThrow(() => new PatAuthenticationHandler(token));
    }

    [Fact]
    public async Task SendAsync_AddsBearerAuthorizationHeader()
    {
        // Arrange
        var token = "gc_pat_abc123";
        using var handler = new PatAuthenticationHandler(token)
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(token);
    }

    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        using var handler = new PatAuthenticationHandler("gc_pat_abc123")
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            invoker.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_SpecialCharsInToken_PreservesVerbatim()
    {
        // Arrange
        var token = "gc_pat_!@#$%^&*()_+-=[]{}|;':\",./<>?";
        using var handler = new PatAuthenticationHandler(token)
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        request.Headers.Authorization.ShouldNotBeNull();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(token);
    }

    private sealed class DummyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}