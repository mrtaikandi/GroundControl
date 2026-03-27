using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Clients;

internal sealed partial class ClientCleanupService : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromDays(1);
    private const int DefaultGracePeriodDays = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientCleanupService> _logger;

    public ClientCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ClientCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _configuration.GetValue("Clients:CleanupInterval", DefaultInterval);
        using var timer = new PeriodicTimer(interval);

        LogStarted(_logger, interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogCleanupFailed(_logger, ex);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }

        LogStopped(_logger);
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        var gracePeriodDays = _configuration.GetValue("Clients:CleanupGracePeriodDays", DefaultGracePeriodDays);

        using var scope = _scopeFactory.CreateScope();
        var clientStore = scope.ServiceProvider.GetRequiredService<IClientStore>();

        var expiredClients = await clientStore.GetExpiredAndDeactivatedAsync(gracePeriodDays, cancellationToken).ConfigureAwait(false);
        if (expiredClients.Count == 0)
        {
            return;
        }

        foreach (var client in expiredClients)
        {
            await clientStore.HardDeleteAsync(client.Id, cancellationToken).ConfigureAwait(false);
        }

        LogCleanupCompleted(_logger, expiredClients.Count, gracePeriodDays);
    }

    [LoggerMessage(1, LogLevel.Information, "Client cleanup service started with interval {Interval}.")]
    private static partial void LogStarted(ILogger<ClientCleanupService> logger, TimeSpan interval);

    [LoggerMessage(2, LogLevel.Information, "Client cleanup: hard-deleted {Count} expired clients (grace period {GracePeriodDays} days).")]
    private static partial void LogCleanupCompleted(ILogger<ClientCleanupService> logger, int count, int gracePeriodDays);

    [LoggerMessage(3, LogLevel.Error, "Client cleanup failed.")]
    private static partial void LogCleanupFailed(ILogger<ClientCleanupService> logger, Exception exception);

    [LoggerMessage(4, LogLevel.Information, "Client cleanup service stopped.")]
    private static partial void LogStopped(ILogger<ClientCleanupService> logger);
}