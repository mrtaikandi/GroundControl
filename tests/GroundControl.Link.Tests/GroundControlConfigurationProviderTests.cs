using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using GroundControl.Link.Internals;

namespace GroundControl.Link.Tests;

public sealed class GroundControlConfigurationProviderTests : IAsyncDisposable
{
    private readonly ISseClient _sseClient = Substitute.For<ISseClient>();
    private readonly IConfigFetcher _configFetcher = Substitute.For<IConfigFetcher>();
    private readonly IConfigCache _configCache = Substitute.For<IConfigCache>();
    private GroundControlConfigurationProvider? _provider;

    public ValueTask DisposeAsync()
    {
        _provider?.Dispose();
        _configCache.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public void Load_SseDeliversConfig_SetsData()
    {
        // Arrange
        var configJson = CreateConfigJson(("Logging:LogLevel:Default", "Warning"), ("Database:Host", "localhost"));
        var events = new[] { new SseEvent { EventType = "config", Data = configJson, Id = "1" } };

        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateSseStream(events, callInfo.Arg<CancellationToken>()));

        _provider = CreateProvider();

        // Act
        _provider.Load();

        // Assert
        _provider.TryGet("Logging:LogLevel:Default", out var logLevel).ShouldBeTrue();
        logLevel.ShouldBe("Warning");
        _provider.TryGet("Database:Host", out var host).ShouldBeTrue();
        host.ShouldBe("localhost");
    }

    [Fact]
    public void Load_SseTimesOut_FallsBackToFetcher()
    {
        // Arrange
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateBlockingSseStream(callInfo.Arg<CancellationToken>()));

        var fetchResult = new FetchResult
        {
            Status = FetchStatus.Success,
            Config = new Dictionary<string, string> { ["Key1"] = "FromRest" },
            ETag = "\"v1\""
        };

        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(fetchResult);

        _provider = CreateProvider(startupTimeout: TimeSpan.FromMilliseconds(200));

        // Act
        _provider.Load();

        // Assert
        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("FromRest");
    }

    [Fact]
    public void Load_SseAndFetcherFail_FallsBackToCache()
    {
        // Arrange
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateBlockingSseStream(callInfo.Arg<CancellationToken>()));

        _configFetcher.FetchAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new FetchResult { Status = FetchStatus.TransientError });

        var cachedConfig = new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Key1"] = "FromCache" }
        };

        _configCache.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(cachedConfig);

        _provider = CreateProvider(startupTimeout: TimeSpan.FromMilliseconds(200));

        // Act
        _provider.Load();

        // Assert
        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("FromCache");
    }

    [Fact]
    public void Load_FetcherThrows_FallsBackToCache()
    {
        // Arrange
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateBlockingSseStream(callInfo.Arg<CancellationToken>()));

        _configFetcher.FetchAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var cachedConfig = new CachedConfiguration
        {
            Entries = new Dictionary<string, string> { ["Key1"] = "FromCache" }
        };

        _configCache.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(cachedConfig);

        _provider = CreateProvider(startupTimeout: TimeSpan.FromMilliseconds(200));

        // Act
        _provider.Load();

        // Assert
        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("FromCache");
    }

    [Fact]
    public async Task Load_SseEventAfterStartup_TriggersOnReload()
    {
        // Arrange
        var initialConfig = CreateConfigJson(("Key1", "Value1"));
        var updatedConfig = CreateConfigJson(("Key1", "UpdatedValue"));

        var eventChannel = Channel.CreateUnbounded<SseEvent>();
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => eventChannel.Reader.ReadAllAsync(callInfo.Arg<CancellationToken>()));

        eventChannel.Writer.TryWrite(new SseEvent { EventType = "config", Data = initialConfig, Id = "1" });

        _provider = CreateProvider();
        _provider.Load();

        // Act — send second config event and wait for OnReload
        var reloadTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changeToken = _provider.GetReloadToken();
        changeToken.RegisterChangeCallback(_ => reloadTriggered.TrySetResult(true), null);

        eventChannel.Writer.TryWrite(new SseEvent { EventType = "config", Data = updatedConfig, Id = "2" });

        // Assert
        var completed = await Task.WhenAny(reloadTriggered.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        completed.ShouldBe(reloadTriggered.Task, "OnReload should have been triggered");

        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("UpdatedValue");
    }

    [Fact]
    public void Load_SseDeliversConfig_SavesConfigToCache()
    {
        // Arrange
        var configJson = CreateConfigJson(("Key1", "Value1"));
        var events = new[] { new SseEvent { EventType = "config", Data = configJson, Id = "1" } };

        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateSseStream(events, callInfo.Arg<CancellationToken>()));

        _provider = CreateProvider();

        // Act
        _provider.Load();

        // Assert — give background task a moment to save cache
        Thread.Sleep(100);
        _configCache.Received().SaveAsync(
            Arg.Is<CachedConfiguration>(c => c.Entries.ContainsKey("Key1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Load_PollingMode_SkipsSse()
    {
        // Arrange
        var fetchResult = new FetchResult
        {
            Status = FetchStatus.Success,
            Config = new Dictionary<string, string> { ["Key1"] = "FromRest" },
            ETag = "\"v1\""
        };

        _configFetcher.FetchAsync(null, Arg.Any<CancellationToken>())
            .Returns(fetchResult);

        _provider = CreateProvider(connectionMode: ConnectionMode.Polling);

        // Act
        _provider.Load();

        // Assert
        _sseClient.DidNotReceive().StreamAsync(Arg.Any<CancellationToken>());
        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("FromRest");
    }

    [Fact]
    public void Load_SseSkipsHeartbeatEvents_WaitsForConfig()
    {
        // Arrange
        var configJson = CreateConfigJson(("Key1", "Value1"));
        SseEvent[] events =
        [
            new() { EventType = "heartbeat", Data = "{}", Id = null },
            new() { EventType = "config", Data = configJson, Id = "1" }
        ];

        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateSseStream(events, callInfo.Arg<CancellationToken>()));

        _provider = CreateProvider();

        // Act
        _provider.Load();

        // Assert
        _provider.TryGet("Key1", out var value).ShouldBeTrue();
        value.ShouldBe("Value1");
    }

    [Fact]
    public void Load_ConfigKeysCaseInsensitive()
    {
        // Arrange
        var configJson = CreateConfigJson(("MySection:MyKey", "MyValue"));
        var events = new[] { new SseEvent { EventType = "config", Data = configJson, Id = "1" } };

        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateSseStream(events, callInfo.Arg<CancellationToken>()));

        _provider = CreateProvider();

        // Act
        _provider.Load();

        // Assert — case-insensitive key lookup
        _provider.TryGet("mysection:mykey", out var value).ShouldBeTrue();
        value.ShouldBe("MyValue");
    }

    [Fact]
    public void ParseConfigDataWithVersion_ValidJson_ReturnsEntriesAndVersion()
    {
        // Arrange
        var json = CreateConfigJson(("Key1", "Value1"), ("Key2", "Value2"));

        // Act
        var (config, snapshotVersion) = GroundControlConfigurationProvider.ParseConfigDataWithVersion(json);

        // Assert
        config.Count.ShouldBe(2);
        config["Key1"].ShouldBe("Value1");
        config["Key2"].ShouldBe("Value2");
        snapshotVersion.ShouldBe("1");
    }

    [Fact]
    public void ParseConfigDataWithVersion_NoEntriesProperty_ReturnsEmpty()
    {
        // Arrange
        var json = "{}";

        // Act
        var (config, snapshotVersion) = GroundControlConfigurationProvider.ParseConfigDataWithVersion(json);

        // Assert
        config.Count.ShouldBe(0);
        snapshotVersion.ShouldBeNull();
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private GroundControlConfigurationProvider CreateProvider(TimeSpan? startupTimeout = null, ConnectionMode connectionMode = ConnectionMode.SseWithPollingFallback)
    {
        var options = new GroundControlOptions
        {
            ServerUrl = "https://test.example.com",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            StartupTimeout = startupTimeout ?? TimeSpan.FromSeconds(5),
            ConnectionMode = connectionMode,
            PollingInterval = TimeSpan.FromHours(1)
        };

        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost") };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{options.ClientId}:{options.ClientSecret}");
        httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, options.ApiVersion);

        return new GroundControlConfigurationProvider(
            httpClient,
            options,
            _sseClient,
            _configFetcher,
            _configCache,
            NullLogger<GroundControlConfigurationProvider>.Instance);
    }

    private static string CreateConfigJson(params (string Key, string Value)[] entries)
    {
        var data = entries.ToDictionary(e => e.Key, e => e.Value);
        return JsonSerializer.Serialize(new { data, snapshotId = Guid.Empty, snapshotVersion = 1 });
    }

    private static async IAsyncEnumerable<SseEvent> CreateSseStream(
        SseEvent[] events,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<SseEvent> CreateBlockingSseStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected — stream cancelled by timeout or shutdown
        }

        yield break;
    }
}