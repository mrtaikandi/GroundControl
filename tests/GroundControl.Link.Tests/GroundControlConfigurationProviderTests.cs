using GroundControl.Link.Internals;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroundControl.Link.Tests;

public sealed class GroundControlConfigurationProviderTests : IDisposable
{
    private readonly IConfigFetcher _configFetcher = Substitute.For<IConfigFetcher>();
    private readonly IConfigCache _configCache = Substitute.For<IConfigCache>();
    private readonly GroundControlStore _store;
    private GroundControlConfigurationProvider? _provider;

    public GroundControlConfigurationProviderTests()
    {
        _store = new GroundControlStore(new GroundControlOptions
        {
            ServerUrl = "http://localhost",
            ClientId = "test",
            ClientSecret = "secret"
        });
    }

    public void Dispose()
    {
        _provider?.Dispose();
        _configCache.Dispose();
    }

    private GroundControlConfigurationProvider CreateProvider()
    {
        _provider = new GroundControlConfigurationProvider(_store, _configCache, _configFetcher);
        return _provider;
    }

    [Fact]
    public void Load_NoCacheAndServerReturnsConfig_SetsData()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["Key1"] = "Value1" },
                ETag = "\"1\""
            });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("Value1");
        _store.HealthStatus.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void Load_CacheExistsAndServerReturns304_UsesCachedData()
    {
        // Arrange
        _configCache.Load().Returns(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Cached"] = "Data" },
            ETag = "\"5\"",
            LastEventId = "evt-1"
        });
        _configFetcher.FetchAsync("\"5\"", Arg.Any<CancellationToken>())
            .Returns(new FetchResult { Status = FetchStatus.NotModified, ETag = "\"5\"" });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Cached", out var value).ShouldBeTrue();
        value.ShouldBe("Data");
        _store.HealthStatus.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void Load_CacheExistsAndServerReturnsNewConfig_UsesServerData()
    {
        // Arrange
        _configCache.Load().Returns(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Old"] = "Stale" },
            ETag = "\"1\""
        });
        _configFetcher.FetchAsync("\"1\"", Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["New"] = "Fresh" },
                ETag = "\"2\""
            });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("New", out var value).ShouldBeTrue();
        value.ShouldBe("Fresh");
        provider.TryGet("Old", out _).ShouldBeFalse();
    }

    [Fact]
    public void Load_CacheExistsAndServerFails_UsesCachedDataAndMarksDegraded()
    {
        // Arrange
        _configCache.Load().Returns(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Cached"] = "Fallback" },
            ETag = "\"3\""
        });
        _configFetcher.FetchAsync("\"3\"", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Cached", out var value).ShouldBeTrue();
        value.ShouldBe("Fallback");
        _store.HealthStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void Load_NoCacheAndServerFails_EmptyDataAndMarksUnhealthy()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("anything", out _).ShouldBeFalse();
        _store.HealthStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Load_ServerSuccess_SavesNewDataToCache()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["K"] = "V" },
                ETag = "\"10\""
            });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        _configCache.Received(1).Save(Arg.Is<CachedConfiguration>(c =>
            c.Entries.ContainsKey("K") && c.ETag == "\"10\""));
    }

    [Fact]
    public void Load_Server304_DoesNotSaveToCache()
    {
        // Arrange
        _configCache.Load().Returns(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["K"] = "V" },
            ETag = "\"5\""
        });
        _configFetcher.FetchAsync("\"5\"", Arg.Any<CancellationToken>())
            .Returns(new FetchResult { Status = FetchStatus.NotModified, ETag = "\"5\"" });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        _configCache.DidNotReceive().Save(Arg.Any<CachedConfiguration>());
    }

    [Fact]
    public void Load_CaseInsensitiveKeyAccess()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["MyKey"] = "MyValue" },
                ETag = "\"1\""
            });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("mykey", out var value).ShouldBeTrue();
        value.ShouldBe("MyValue");
    }

    [Fact]
    public void OnDataChanged_TriggersReloadAndUpdatesData()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["Initial"] = "1" },
                ETag = "\"1\""
            });
        var provider = CreateProvider();
        provider.Load();

        var reloadTriggered = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadTriggered = true, null);

        // Act — simulate Phase 2 background service pushing new data
        _store.Update(
            new Dictionary<string, string> { ["Updated"] = "2" },
            "\"2\"",
            "evt-1");

        // Assert
        provider.TryGet("Updated", out var value).ShouldBeTrue();
        value.ShouldBe("2");
        provider.TryGet("Initial", out _).ShouldBeFalse();
        reloadTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromStoreEvents()
    {
        // Arrange
        _configCache.Load().Returns((CachedConfiguration?)null);
        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["K"] = "V" },
                ETag = "\"1\""
            });
        var provider = CreateProvider();
        provider.Load();

        // Act
        provider.Dispose();
        _store.Update(new Dictionary<string, string> { ["After"] = "Dispose" }, "\"2\"", null);

        // Assert — Data should NOT have been updated after disposal
        provider.TryGet("After", out _).ShouldBeFalse();
        provider.TryGet("K", out _).ShouldBeTrue();
    }

    [Fact]
    public void Load_ServerTransientError_UsesCachedDataAndMarksDegraded()
    {
        // Arrange
        _configCache.Load().Returns(new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Cached"] = "Value" },
            ETag = "\"1\""
        });
        _configFetcher.FetchAsync("\"1\"", Arg.Any<CancellationToken>())
            .Returns(new FetchResult { Status = FetchStatus.TransientError });
        var provider = CreateProvider();

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Cached", out var value).ShouldBeTrue();
        value.ShouldBe("Value");
        _store.HealthStatus.ShouldBe(HealthStatus.Degraded);
    }
}