using GroundControl.Cli.Shared.Auth;
using Microsoft.Extensions.Time.Testing;

namespace GroundControl.Cli.Tests.Shared.Auth;

public sealed class TokenCacheTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly TokenCache _cache;

    public TokenCacheTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _cache = new TokenCache(_timeProvider);
    }

    [Fact]
    public void AccessToken_WhenEmpty_ReturnsNull()
    {
        // Act
        var result = _cache.AccessToken;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void RefreshToken_WhenEmpty_ReturnsNull()
    {
        // Act
        var result = _cache.RefreshToken;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void HasValidAccessToken_WhenEmpty_ReturnsFalse()
    {
        // Act & Assert
        _cache.HasValidAccessToken.ShouldBeFalse();
    }

    [Fact]
    public void HasValidRefreshToken_WhenEmpty_ReturnsFalse()
    {
        // Act & Assert
        _cache.HasValidRefreshToken.ShouldBeFalse();
    }

    [Fact]
    public void AccessToken_AfterSetTokens_ReturnsAccessToken()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        var result = _cache.AccessToken;

        // Assert
        result.ShouldBe("access-123");
    }

    [Fact]
    public void RefreshToken_AfterSetTokens_ReturnsRefreshToken()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        var result = _cache.RefreshToken;

        // Assert
        result.ShouldBe("refresh-456");
    }

    [Fact]
    public void HasValidAccessToken_AfterSetTokens_ReturnsTrue()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act & Assert
        _cache.HasValidAccessToken.ShouldBeTrue();
    }

    [Fact]
    public void HasValidRefreshToken_AfterSetTokens_ReturnsTrue()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act & Assert
        _cache.HasValidRefreshToken.ShouldBeTrue();
    }

    [Fact]
    public void HasValidAccessToken_WhenExpired_ReturnsFalse()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act — advance past expiry
        _timeProvider.Advance(TimeSpan.FromSeconds(3601));

        // Assert
        _cache.HasValidAccessToken.ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_WithinExpiryBuffer_ReturnsFalse()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act — advance to within 30-second buffer of expiry
        _timeProvider.Advance(TimeSpan.FromSeconds(3571));

        // Assert — should proactively consider expired
        _cache.HasValidAccessToken.ShouldBeFalse();
    }

    [Fact]
    public void HasValidAccessToken_JustBeforeBuffer_ReturnsTrue()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act — advance to just before the buffer window
        _timeProvider.Advance(TimeSpan.FromSeconds(3569));

        // Assert
        _cache.HasValidAccessToken.ShouldBeTrue();
    }

    [Fact]
    public void HasValidRefreshToken_WhenExpired_ReturnsFalse()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(86401));

        // Assert
        _cache.HasValidRefreshToken.ShouldBeFalse();
    }

    [Fact]
    public void HasValidRefreshToken_WithinExpiryBuffer_ReturnsFalse()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(86371));

        // Assert
        _cache.HasValidRefreshToken.ShouldBeFalse();
    }

    [Fact]
    public void AccessToken_WhenExpired_ReturnsNull()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(3601));

        // Assert
        _cache.AccessToken.ShouldBeNull();
    }

    [Fact]
    public void RefreshToken_WhenExpired_ReturnsNull()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(86401));

        // Assert
        _cache.RefreshToken.ShouldBeNull();
    }

    [Fact]
    public void Clear_RemovesAllTokens()
    {
        // Arrange
        _cache.SetTokens("access-123", "refresh-456", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _cache.Clear();

        // Assert
        _cache.AccessToken.ShouldBeNull();
        _cache.RefreshToken.ShouldBeNull();
        _cache.HasValidAccessToken.ShouldBeFalse();
        _cache.HasValidRefreshToken.ShouldBeFalse();
    }

    [Fact]
    public void SetTokens_OverwritesPreviousTokens()
    {
        // Arrange
        _cache.SetTokens("old-access", "old-refresh", expiresInSeconds: 3600, refreshExpiresInSeconds: 86400);

        // Act
        _cache.SetTokens("new-access", "new-refresh", expiresInSeconds: 7200, refreshExpiresInSeconds: 172800);

        // Assert
        _cache.AccessToken.ShouldBe("new-access");
        _cache.RefreshToken.ShouldBe("new-refresh");
    }

    [Fact]
    public async Task WithRefreshLockAsync_SerializesAccess()
    {
        // Arrange
        var entryOrder = new List<int>();
        var barrier = new TaskCompletionSource();
        var ct = TestContext.Current.CancellationToken;

        // Act — start two concurrent operations
        var task1 = _cache.WithRefreshLockAsync(async () =>
        {
            entryOrder.Add(1);
            await barrier.Task;
            return 1;
        }, ct);

        // Give task1 time to acquire the lock
        await Task.Delay(50, ct);

        var task2 = _cache.WithRefreshLockAsync(async () =>
        {
            entryOrder.Add(2);
            return 2;
        }, ct);

        // Assert — task2 should be blocked, only task1 has entered
        entryOrder.ShouldBe([1]);

        // Release task1
        barrier.SetResult();
        await Task.WhenAll(task1, task2);

        // Assert — both completed in order
        entryOrder.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task WithRefreshLockAsync_PropagatesCancellation()
    {
        // Arrange
        using var holdCts = new CancellationTokenSource();
        using var waitCts = new CancellationTokenSource();
        var entered = new TaskCompletionSource();

        // Hold the lock with a task we can cancel to release it
        var holdTask = _cache.WithRefreshLockAsync(async () =>
        {
            entered.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), holdCts.Token);
            return 0;
        }, holdCts.Token);

        await entered.Task;

        // Act & Assert — cancelling while waiting for lock
        await waitCts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(
            _cache.WithRefreshLockAsync(async () =>
            {
                await Task.CompletedTask;
                return 0;
            }, waitCts.Token));

        // Cleanup — cancel the lock-holding task so the semaphore is released before Dispose
        await holdCts.CancelAsync();
        try
        {
            await holdTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    public void Dispose() => _cache.Dispose();
}