namespace GroundControl.Api.Shared.Security.Certificate;

/// <summary>
/// Loads the Data Protection certificate at startup to verify it is accessible
/// and to log the certificate thumbprint.
/// </summary>
internal sealed partial class CertificateStartupLogger(
    IDataProtectionCertificateProvider provider,
    ILogger<CertificateStartupLogger> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var certificate = await provider.GetCurrentCertificateAsync(cancellationToken).ConfigureAwait(false);
        LogCertificateReady(logger, certificate.Thumbprint);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(1, LogLevel.Information, "Data Protection certificate ready with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateReady(ILogger logger, string thumbprint);
}