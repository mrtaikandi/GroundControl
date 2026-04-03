namespace GroundControl.Cli.Shared.Auth;

internal sealed class TokenCache : IDisposable
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset _accessTokenExpiry;
    private DateTimeOffset _refreshTokenExpiry;

    public TokenCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool HasValidAccessToken =>
        _accessToken is not null && _timeProvider.GetUtcNow() < _accessTokenExpiry - ExpiryBuffer;

    public bool HasValidRefreshToken =>
        _refreshToken is not null && _timeProvider.GetUtcNow() < _refreshTokenExpiry - ExpiryBuffer;

    public bool WasPopulated { get; private set; }

    public string? AccessToken => HasValidAccessToken ? _accessToken : null;

    public string? RefreshToken => HasValidRefreshToken ? _refreshToken : null;

    public void SetTokens(string accessToken, string refreshToken, int expiresInSeconds, int refreshExpiresInSeconds)
    {
        var now = _timeProvider.GetUtcNow();
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _accessTokenExpiry = now.AddSeconds(expiresInSeconds);
        _refreshTokenExpiry = now.AddSeconds(refreshExpiresInSeconds);
        WasPopulated = true;
    }

    public void Clear()
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = default;
        _refreshTokenExpiry = default;
        WasPopulated = false;
    }

    public async Task<T> WithRefreshLockAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}