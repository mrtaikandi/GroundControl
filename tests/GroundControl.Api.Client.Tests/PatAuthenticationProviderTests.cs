using Microsoft.Kiota.Abstractions;

namespace GroundControl.Api.Client.Tests;

public sealed class PatAuthenticationProviderTests
{
    [Fact]
    public void Constructor_NullToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PatAuthenticationProvider(null!));
    }

    [Fact]
    public void Constructor_EmptyToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationProvider(string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceToken_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationProvider("   "));
    }

    [Fact]
    public void Constructor_MissingPrefix_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => new PatAuthenticationProvider("some_token_value"));
        exception.Message.ShouldContain("gc_pat_");
    }

    [Fact]
    public void Constructor_WrongCasePrefix_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new PatAuthenticationProvider("GC_PAT_some_token"));
    }

    [Fact]
    public void Constructor_ValidToken_DoesNotThrow()
    {
        // Arrange
        var token = "gc_pat_abc123";

        // Act & Assert
        Should.NotThrow(() => new PatAuthenticationProvider(token));
    }

    [Fact]
    public void Constructor_PrefixOnly_DoesNotThrow()
    {
        // Arrange
        var token = "gc_pat_";

        // Act & Assert
        Should.NotThrow(() => new PatAuthenticationProvider(token));
    }

    [Fact]
    public async Task AuthenticateRequestAsync_AddsBearerAuthorizationHeader()
    {
        // Arrange
        var token = "gc_pat_abc123";
        var provider = new PatAuthenticationProvider(token);
        var request = new RequestInformation();

        // Act
        await provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        request.Headers["Authorization"].ShouldContain($"Bearer {token}");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new PatAuthenticationProvider("gc_pat_abc123");

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
            provider.AuthenticateRequestAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AuthenticateRequestAsync_ReturnsCompletedTask()
    {
        // Arrange
        var provider = new PatAuthenticationProvider("gc_pat_abc123");
        var request = new RequestInformation();

        // Act
        var task = provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthenticateRequestAsync_SpecialCharsInToken_PreservesVerbatim()
    {
        // Arrange
        var token = "gc_pat_!@#$%^&*()_+-=[]{}|;':\",./<>?";
        var provider = new PatAuthenticationProvider(token);
        var request = new RequestInformation();

        // Act
        await provider.AuthenticateRequestAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        request.Headers["Authorization"].ShouldContain($"Bearer {token}");
    }
}