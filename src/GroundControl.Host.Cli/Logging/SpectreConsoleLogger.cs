using Microsoft.Extensions.Logging;

namespace GroundControl.Host.Cli.Logging;

internal sealed class SpectreConsoleLogger(string categoryName, SpectreConsoleLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        provider.WriteLogEntry(logLevel, categoryName, message, exception);
    }
}