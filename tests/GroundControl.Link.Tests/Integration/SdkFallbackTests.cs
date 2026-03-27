using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Link.Tests.Integration;

public sealed class SdkFallbackTests : IDisposable
{
    private readonly string _cacheDir;

    public SdkFallbackTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "groundcontrol-sdk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task Sdk_ServerUnreachable_FallsBackToCache()
    {
        // Arrange — pre-populate cache file
        var cachePath = Path.Combine(_cacheDir, "fallback.cache.json");
        var cache = CreateFileConfigCache(cachePath);
        var config = new Dictionary<string, string>
        {
            ["app.name"] = "CachedApp",
            ["app.version"] = "1.0.0"
        };

        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        var options = new GroundControlOptions
        {
            ServerUrl = "http://localhost:1",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = TimeSpan.FromSeconds(1),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            PollingInterval = TimeSpan.FromHours(1),
            CacheFilePath = cachePath,
            EnableLocalCache = true,
        };

        using var provider = CreateProviderWithUnreachableServer(options);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("app.name", out var name).ShouldBeTrue();
        name.ShouldBe("CachedApp");
        provider.TryGet("app.version", out var version).ShouldBeTrue();
        version.ShouldBe("1.0.0");
    }

    [Fact]
    public void Sdk_NoServerAndNoCache_StartsWithEmptyConfig()
    {
        // Arrange — no cache file exists, server is unreachable
        var cachePath = Path.Combine(_cacheDir, "nonexistent.cache.json");

        var options = new GroundControlOptions
        {
            ServerUrl = "http://localhost:1",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = TimeSpan.FromSeconds(1),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            PollingInterval = TimeSpan.FromHours(1),
            CacheFilePath = cachePath,
            EnableLocalCache = true,
        };

        using var provider = CreateProviderWithUnreachableServer(options);

        // Act — should not throw
        provider.Load();

        // Assert — empty config, no keys set
        provider.TryGet("any.key", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Sdk_PollingMode_ServerUnreachable_FallsBackToCache()
    {
        // Arrange — pre-populate cache file
        var cachePath = Path.Combine(_cacheDir, "polling-fallback.cache.json");
        var cache = CreateFileConfigCache(cachePath);
        var config = new Dictionary<string, string> { ["key1"] = "cached-value" };
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        var options = new GroundControlOptions
        {
            ServerUrl = "http://localhost:1",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = TimeSpan.FromSeconds(1),
            ConnectionMode = ConnectionMode.Polling,
            PollingInterval = TimeSpan.FromHours(1),
            CacheFilePath = cachePath,
            EnableLocalCache = true,
        };

        using var provider = CreateProviderWithUnreachableServer(options);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("key1", out var value).ShouldBeTrue();
        value.ShouldBe("cached-value");
    }

    [Fact]
    public async Task Sdk_SseOnlyMode_ServerUnreachable_FallsBackToCache()
    {
        // Arrange
        var cachePath = Path.Combine(_cacheDir, "sse-only-fallback.cache.json");
        var cache = CreateFileConfigCache(cachePath);
        var config = new Dictionary<string, string> { ["key1"] = "sse-cached" };
        await cache.SaveAsync(config, TestContext.Current.CancellationToken);

        var options = new GroundControlOptions
        {
            ServerUrl = "http://localhost:1",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = TimeSpan.FromSeconds(1),
            ConnectionMode = ConnectionMode.Sse,
            PollingInterval = TimeSpan.FromHours(1),
            CacheFilePath = cachePath,
            EnableLocalCache = true,
        };

        using var provider = CreateProviderWithUnreachableServer(options);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("key1", out var value).ShouldBeTrue();
        value.ShouldBe("sse-cached");
    }

    [Fact]
    public void Sdk_NoServerAndNoCache_PollingMode_StartsWithEmptyConfig()
    {
        // Arrange
        var cachePath = Path.Combine(_cacheDir, "no-cache-polling.cache.json");

        var options = new GroundControlOptions
        {
            ServerUrl = "http://localhost:1",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = TimeSpan.FromSeconds(1),
            ConnectionMode = ConnectionMode.Polling,
            PollingInterval = TimeSpan.FromHours(1),
            CacheFilePath = cachePath,
            EnableLocalCache = true,
        };

        using var provider = CreateProviderWithUnreachableServer(options);

        // Act — should not throw
        provider.Load();

        // Assert
        provider.TryGet("any.key", out _).ShouldBeFalse();
    }

    private static FileConfigCache CreateFileConfigCache(string cachePath) =>
        new(
            new GroundControlOptions
            {
                ServerUrl = "http://localhost",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                CacheFilePath = cachePath,
            },
            NullLogger<FileConfigCache>.Instance);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Provider owns and disposes SSE client and HttpClient via Dispose")]
    private static GroundControlConfigurationProvider CreateProviderWithUnreachableServer(GroundControlOptions options)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(options.ServerUrl) };

        var sseClient = new DefaultSseClient(httpClient, options, NullLogger<DefaultSseClient>.Instance);
        var configFetcher = new DefaultConfigFetcher(httpClient, options, NullLogger<DefaultConfigFetcher>.Instance);
        IConfigCache configCache = options.EnableLocalCache
            ? new FileConfigCache(options, NullLogger<FileConfigCache>.Instance)
            : NullConfigCache.Instance;

        return new GroundControlConfigurationProvider(
            options,
            sseClient,
            configFetcher,
            configCache,
            NullLogger<GroundControlConfigurationProvider>.Instance);
    }
}