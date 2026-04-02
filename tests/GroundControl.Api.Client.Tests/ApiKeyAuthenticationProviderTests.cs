using Microsoft.Kiota.Abstractions;

namespace GroundControl.Api.Client.Tests;

public sealed class ApiKeyAuthenticationProviderTests
{
    [Fact]
    public void Constructor_NullSecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ApiKeyAuthenticationProvider(clientId, null!));
    }

    [Fact]
    public void Constructor_EmptySecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiKeyAuthenticationProvider(clientId, string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceSecret_ThrowsArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiKeyAuthenticationProvider(clientId, "   "));
    }

    [Fact]
    public void Constructor_ValidCredentials_DoesNotThrow()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var clientSecret = "my-secret";

        // Act & Assert
        Should.NotThrow(() => new ApiKeyAuthenticationProvider(clientId, clientSecret));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_AddsApiKeyAuthorizationHeader()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var clientSecret = "my-secret";
        var provider = new ApiKeyAuthenticationProvider(clientId, clientSecret);
        var request = new RequestInformation();

        // Act
        await provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        request.Headers["Authorization"].ShouldContain($"ApiKey {clientId}:{clientSecret}");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_EmptyGuid_FormatsCorrectly()
    {
        // Arrange
        var clientId = Guid.Empty;
        var clientSecret = "my-secret";
        var provider = new ApiKeyAuthenticationProvider(clientId, clientSecret);
        var request = new RequestInformation();

        // Act
        await provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        request.Headers["Authorization"].ShouldContain($"ApiKey {Guid.Empty}:{clientSecret}");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(Guid.NewGuid(), "my-secret");

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            provider.AuthenticateRequestAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AuthenticateRequestAsync_ReturnsCompletedTask()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(Guid.NewGuid(), "my-secret");
        var request = new RequestInformation();

        // Act
        var task = provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        task.IsCompleted.ShouldBeTrue();
    }
}