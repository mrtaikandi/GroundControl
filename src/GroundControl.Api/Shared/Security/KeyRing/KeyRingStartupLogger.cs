namespace GroundControl.Api.Shared.Security.KeyRing;

/// <summary>
/// Logs the active Data Protection key ring mode at startup.
/// </summary>
internal sealed partial class KeyRingStartupLogger(
    string mode,
    ILogger<KeyRingStartupLogger> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogKeyRingMode(logger, mode);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(1, LogLevel.Information, "Data Protection key ring mode: {Mode}.")]
    private static partial void LogKeyRingMode(ILogger logger, string mode);
}