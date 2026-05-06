namespace GroundControl.Api.Core.DataProtection.Certificate;

/// <summary>
/// Loads the Data Protection certificate at startup to verify it is accessible
/// and to log the certificate thumbprint.
/// </summary>
internal sealed partial class CertificateStartupLogger(IDataProtectionCertificateProvider provider, ILogger<CertificateStartupLogger> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var certificate = provider.GetCurrentCertificate();
        LogCertificateReady(logger, certificate.Thumbprint);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(1, LogLevel.Information, "Data Protection certificate ready with thumbprint {Thumbprint}.")]
    private static partial void LogCertificateReady(ILogger logger, string thumbprint);
}