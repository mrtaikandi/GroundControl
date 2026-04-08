using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using GroundControl.Link.Internals;
using Microsoft.Extensions.DependencyInjection;

// Cancellation tokens flow through IAsyncEnumerable.GetAsyncEnumerator via [EnumeratorCancellation]
#pragma warning disable xUnit1051

namespace GroundControl.Link.Tests.Internals;

public sealed class SseConnectionStrategyTests : IDisposable
{
    private readonly IGroundControlSseClient _sseClient = Substitute.For<IGroundControlSseClient>();
    private readonly IConfigurationCache _cache = Substitute.For<IConfigurationCache>();
    private readonly ServiceProvider _serviceProvider;
    private readonly GroundControlMetrics _metrics;
    private readonly GroundControlStore _store;

    public SseConnectionStrategyTests()
    {
        _store = new GroundControlStore(new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost"),
            ClientId = "test",
            ClientSecret = "secret",
            SseReconnectDelay = TimeSpan.FromMilliseconds(50),
            SseMaxReconnectDelay = TimeSpan.FromMilliseconds(200)
        });

        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        _metrics = new GroundControlMetrics(_serviceProvider.GetRequiredService<IMeterFactory>());
    }

    public void Dispose()
    {
        _cache.Dispose();
        _metrics.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_SseDeliversConfig_UpdatesStore()
    {
        // Arrange
        var json = """{"data":{"Key1":"Value1"},"snapshotVersion":1}""";
        var events = new[] { new SseEvent { EventType = "config", Data = json, Id = "evt-1" } };
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(events));

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert
        var snapshot = _store.GetSnapshot();
        snapshot.Data.ShouldContainKeyAndValue("Key1", "Value1");
        snapshot.ETag.ShouldBe("1");
        snapshot.LastEventId.ShouldBe("evt-1");
    }

    [Fact]
    public async Task ExecuteAsync_SseDeliversConfig_SavesToCache()
    {
        // Arrange
        var json = """{"data":{"K":"V"},"snapshotVersion":1}""";
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable([new SseEvent { EventType = "config", Data = json, Id = "e1" }]));

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert
        await _cache.Received().SaveAsync(
            Arg.Is<CachedConfiguration>(c => c.Entries.ContainsKey("K")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsHeartbeatEvents()
    {
        // Arrange
        var events = new[]
        {
            new SseEvent { EventType = "heartbeat", Data = "" },
            new SseEvent { EventType = "config", Data = """{"data":{"K":"V"},"snapshotVersion":1}""", Id = "e1" }
        };
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(events));

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert
        _store.GetSnapshot().Data.ShouldContainKeyAndValue("K", "V");
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_StopsGracefully()
    {
        // Arrange
        _sseClient.StreamAsync(Arg.Any<CancellationToken>())
            .Returns(CreateBlockingStream());

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act & Assert -- should not throw
        await strategy.ExecuteAsync(_store, cts.Token);
    }

    private SseConnectionStrategy CreateStrategy() =>
        new(_sseClient, _cache, NullLogger<SseConnectionStrategy>.Instance, _metrics);

    private static async IAsyncEnumerable<SseEvent> ToAsyncEnumerable(
        SseEvent[] events,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    private static async IAsyncEnumerable<SseEvent> CreateBlockingStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        yield break;
    }
}