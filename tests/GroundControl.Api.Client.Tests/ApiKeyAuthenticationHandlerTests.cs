using System.Net;
using GroundControl.Api.Client.Handlers;

namespace GroundControl.Api.Client.Tests;

public sealed class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public void Constructor_NullSecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ApiKeyAuthenticationHandler(clientId, null!));
    }

    [Fact]
    public void Constructor_EmptySecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiKeyAuthenticationHandler(clientId, string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceSecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiKeyAuthenticationHandler(clientId, "   "));
    }

    [Fact]
    public void Constructor_ValidCredentials_DoesNotThrow()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var clientSecret = "my-secret";

        // Act & Assert
        Should.NotThrow(() => new ApiKeyAuthenticationHandler(clientId, clientSecret));
    }

    [Fact]
    public async Task SendAsync_AddsApiKeyAuthorizationHeader()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var clientSecret = "my-secret";
        using var handler = new ApiKeyAuthenticationHandler(clientId, clientSecret)
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var authValues = request.Headers.GetValues("Authorization").ToList();
        authValues.ShouldContain($"ApiKey {clientId}:{clientSecret}");
    }

    [Fact]
    public async Task SendAsync_EmptyGuid_FormatsCorrectly()
    {
        // Arrange
        var clientId = Guid.Empty;
        var clientSecret = "my-secret";
        using var handler = new ApiKeyAuthenticationHandler(clientId, clientSecret)
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var authValues = request.Headers.GetValues("Authorization").ToList();
        authValues.ShouldContain($"ApiKey {Guid.Empty}:{clientSecret}");
    }

    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        using var handler = new ApiKeyAuthenticationHandler(Guid.NewGuid(), "my-secret")
        {
            InnerHandler = new DummyHandler()
        };
        using var invoker = new HttpMessageInvoker(handler);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            invoker.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    private sealed class DummyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}