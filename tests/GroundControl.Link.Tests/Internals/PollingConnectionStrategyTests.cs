using System.Diagnostics.Metrics;
using GroundControl.Link.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace GroundControl.Link.Tests.Internals;

public sealed class PollingConnectionStrategyTests : IDisposable
{
    private readonly IConfigFetcher _fetcher = Substitute.For<IConfigFetcher>();
    private readonly IConfigCache _cache = Substitute.For<IConfigCache>();
    private readonly ServiceProvider _serviceProvider;
    private readonly GroundControlMetrics _metrics;
    private readonly GroundControlStore _store;

    public PollingConnectionStrategyTests()
    {
        _store = new GroundControlStore(new GroundControlOptions
        {
            ServerUrl = new Uri("http://localhost"),
            ClientId = "test",
            ClientSecret = "secret",
            PollingInterval = TimeSpan.FromMilliseconds(50)
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
    public async Task ExecuteAsync_FetchSuccess_UpdatesStore()
    {
        // Arrange
        var callCount = 0;
        _fetcher.FetchAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new FetchResult
                {
                    Status = FetchStatus.Success,
                    Config = new Dictionary<string, string> { ["K"] = "V" },
                    ETag = "\"1\""
                };
            });

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert
        _store.GetSnapshot().Data.ShouldContainKeyAndValue("K", "V");
        callCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_FetchNotModified_DoesNotUpdateStore()
    {
        // Arrange
        _store.Update(new Dictionary<string, string> { ["Existing"] = "Data" }, "\"1\"", null);
        _fetcher.FetchAsync("\"1\"", Arg.Any<CancellationToken>())
            .Returns(new FetchResult { Status = FetchStatus.NotModified, ETag = "\"1\"" });

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert -- data unchanged, still has "Existing"
        _store.GetSnapshot().Data.ShouldContainKeyAndValue("Existing", "Data");
    }

    [Fact]
    public async Task ExecuteAsync_AuthError_StopsPolling()
    {
        // Arrange
        var callCount = 0;
        _fetcher.FetchAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new FetchResult { Status = FetchStatus.AuthenticationError };
            });

        var strategy = CreateStrategy();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // Act
        await strategy.ExecuteAsync(_store, cts.Token);

        // Assert -- should stop after first auth error, not retry
        callCount.ShouldBe(1);
    }

    private PollingConnectionStrategy CreateStrategy() =>
        new(_fetcher, _cache, NullLogger<PollingConnectionStrategy>.Instance, _metrics);
}