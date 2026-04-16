using Microsoft.Extensions.Options;

namespace GroundControl.Samples.LinkConsole;

/// <summary>
/// A background service that monitors GroundControl configuration changes
/// and logs updates to the console.
/// </summary>
internal sealed partial class ConfigurationMonitorService : BackgroundService
{
    private readonly IOptionsMonitor<SampleSettings> _settingsMonitor;
    private readonly ILogger<ConfigurationMonitorService> _logger;

    public ConfigurationMonitorService(IOptionsMonitor<SampleSettings> settingsMonitor, ILogger<ConfigurationMonitorService> logger)
    {
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogCurrentSettings("Initial configuration loaded");

        var subscription = _settingsMonitor.OnChange(_ =>
        {
            LogCurrentSettings("Configuration updated");
        });

        if (subscription is not null)
        {
            stoppingToken.Register(subscription.Dispose);
        }

        return Task.CompletedTask;
    }

    private void LogCurrentSettings(string reason)
    {
        var settings = _settingsMonitor.CurrentValue;
        LogSettingsChanged(reason, settings.AppName, settings.MaxRetries, settings.DarkMode);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Reason} — AppName: {AppName}, MaxRetries: {MaxRetries}, DarkMode: {DarkMode}")]
    private partial void LogSettingsChanged(string reason, string appName, int maxRetries, bool darkMode);
}