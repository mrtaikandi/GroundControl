using System.Diagnostics.CodeAnalysis;
using GroundControl.Link.Internals;

namespace GroundControl.Link.Tests.Integration;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test helper objects are short-lived; provider disposal handles cleanup")]
public sealed class SdkFallbackTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly string _cachePath;

    public SdkFallbackTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "groundcontrol-sdk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);
        _cachePath = Path.Combine(_cacheDir, "test-cache.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, true);
        }
    }

    [Fact]
    public void Sdk_ServerUnreachable_FallsBackToCache()
    {
        // Arrange — write cache, then create provider pointing at unreachable server
        var options = CreateOptions(enableCache: true);
        var cache = new FileConfigurationCache(options, NullLogger<FileConfigurationCache>.Instance);
        cache.Save(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Cached:Key"] = "CachedValue" },
            ETag = "\"1\""
        });

        using var provider = CreateProviderWithUnreachableServer(options, cache);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Cached:Key", out var value).ShouldBeTrue();
        value.ShouldBe("CachedValue");
    }

    [Fact]
    public void Sdk_NoServerAndNoCache_StartsWithEmptyConfig()
    {
        // Arrange
        var options = CreateOptions(enableCache: false);
        using var provider = CreateProviderWithUnreachableServer(options, NullConfigurationCache.Instance);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("anything", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ConnectionMode.Polling)]
    [InlineData(ConnectionMode.SseWithPollingFallback)]
    [InlineData(ConnectionMode.Sse)]
    public void Sdk_AllModes_ServerUnreachable_FallsBackToCache(ConnectionMode mode)
    {
        // Arrange
        var options = CreateOptions(enableCache: true);
        options.ConnectionMode = mode;
        var cache = new FileConfigurationCache(options, NullLogger<FileConfigurationCache>.Instance);
        cache.Save(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Mode"] = mode.ToString() },
            ETag = "\"1\""
        });

        using var provider = CreateProviderWithUnreachableServer(options, cache);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Mode", out var value).ShouldBeTrue();
        value.ShouldBe(mode.ToString());
    }

    private GroundControlOptions CreateOptions(bool enableCache) => new()
    {
        ServerUrl = new Uri("http://localhost:1"),
        ClientId = "test-client",
        ClientSecret = "test-secret",
        EnableLocalCache = enableCache,
        CacheFilePath = _cachePath,
        StartupTimeout = TimeSpan.FromSeconds(2),
        ConnectionMode = ConnectionMode.SseWithPollingFallback
    };

    private static GroundControlConfigurationProvider CreateProviderWithUnreachableServer(
        GroundControlOptions options,
        IConfigurationCache cache)
    {
        var store = new GroundControlStore(options);
        var httpClient = new HttpClient { BaseAddress = options.ServerUrl, Timeout = TimeSpan.FromSeconds(1) };
        var apiClient = new GroundControlApiClient(httpClient, NullLogger<GroundControlApiClient>.Instance);
        return new GroundControlConfigurationProvider(store, cache, apiClient);
    }
}