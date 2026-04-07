using Microsoft.Extensions.Hosting;

namespace GroundControl.Link.Internals;

/// <summary>
/// Thin background service that delegates to the configured <see cref="IConnectionStrategy"/>.
/// </summary>
internal sealed class GroundControlBackgroundService : BackgroundService
{
    private readonly GroundControlStore _store;
    private readonly IConnectionStrategy _strategy;

    public GroundControlBackgroundService(GroundControlStore store, IConnectionStrategy strategy)
    {
        _store = store;
        _strategy = strategy;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _strategy.ExecuteAsync(_store, stoppingToken);
}