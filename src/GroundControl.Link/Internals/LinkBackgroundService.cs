using Microsoft.Extensions.Hosting;

namespace GroundControl.Link.Internals;

/// <summary>
/// Thin background service that delegates to the configured <see cref="IConnectionStrategy"/>.
/// </summary>
internal sealed class LinkBackgroundService : BackgroundService
{
    private readonly GroundControlStore _store;
    private readonly IConnectionStrategy _strategy;

    public LinkBackgroundService(GroundControlStore store, IConnectionStrategy strategy)
    {
        _store = store;
        _strategy = strategy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) =>
        await _strategy.ExecuteAsync(_store, stoppingToken).ConfigureAwait(false);
}