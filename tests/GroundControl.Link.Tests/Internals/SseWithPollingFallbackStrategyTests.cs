using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

// Cancellation tokens flow through IAsyncEnumerable.GetAsyncEnumerator via [EnumeratorCancellation]
#pragma warning disable xUnit1051

namespace GroundControl.Link.Tests.Internals;

public sealed class SseWithPollingFallbackStrategyTests : IDisposable
{
    private readonly IGroundControlSseClient _sseClient = Substitute.For<IGroundControlSseClient>();
    private readonly IGroundControlApiClient _client = Substitute.For<IGroundControlApiClient>();
    private readonly IConfigurationCache _cache = Substitute.For<IConfigurationCache>();
    private readonly ServiceProvider _serviceProvider;
    private readonly GroundControlMetrics _metrics;
    private readonly GroundControlStore _store;

    public SseWithPollingFallbackStrategyTests()
    {
        _store = new GroundControlStore(new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost"),
            ClientId = "test",
            ClientSecret = "secret",
            PollingInterval = TimeSpan.FromMilliseconds(50),
            SseReconnectDelay = TimeSpan.FromMilliseconds(100),
            SseMaxReconnectDelay = TimeSpan.FromMilliseconds(200)
        });

        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .AddSingleton(_client)
            .AddSingleton(_cache)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<GroundControlMetrics>()
            .AddTransient<PollingConnectionStrategy>()
            .BuildServiceProvider();

        _metrics = _serviceProvider.GetRequiredService<GroundControlMetrics>();
    }

    public void Dispose()
    {
        _cache.Dispose();
        _metrics.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_SseStaysConnected_DoesNotPoll()
    {
        // Arrange -- SSE delivers events then stays open until cancellation
        var json = """{"data":{"K":"V"},"snapshotVersion":1}""";
        var events = new[] { new SseEvent { EventType = "config", Data = json, Id = "e1" } };
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(YieldThenBlock(events));

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert -- data delivered via SSE, no polling occurred
        _store.GetSnapshot().Data.ShouldContainKeyAndValue("K", "V");
        await _client.DidNotReceive().FetchConfigAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SseFails_FallsBackToPolling()
    {
        // Arrange -- SSE fails immediately by yielding an exception from the stream
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(CreateThrowingStream());

        _client.FetchConfigAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new FetchResult
            {
                Status = FetchStatus.Success,
                Config = new Dictionary<string, string> { ["Poll"] = "Data" },
                ETag = "\"1\""
            });

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert -- fetcher was called as fallback
        await _client.Received().FetchConfigAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private SseWithPollingFallbackStrategy CreateStrategy() =>
        new(_sseClient, _cache, NullLogger<SseWithPollingFallbackStrategy>.Instance, _metrics, _serviceProvider.GetRequiredService<PollingConnectionStrategy>());

    /// <summary>
    /// Yields the given events then blocks until cancellation, simulating a persistent SSE connection.
    /// </summary>
    private static async IAsyncEnumerable<SseEvent> YieldThenBlock(
        SseEvent[] events,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }

        // Simulate an open SSE connection waiting for more events
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }

#pragma warning disable CS1998, CS0162 // Async iterator requires yield; unreachable code after throw
    private static async IAsyncEnumerable<SseEvent> CreateThrowingStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        throw new HttpRequestException("SSE failed");
        yield break;
    }
#pragma warning restore CS1998, CS0162
}