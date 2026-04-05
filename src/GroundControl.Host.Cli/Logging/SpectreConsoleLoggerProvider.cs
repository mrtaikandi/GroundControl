using System.Collections.Concurrent;
using GroundControl.Host.Cli.Extensions.Spectre;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace GroundControl.Host.Cli.Logging;

/// <summary>
/// Provides loggers that write to the <see cref="Spectre.Console"/> console.
/// This class is thread-safe and can be used concurrently across multiple threads.
/// </summary>
public sealed class SpectreConsoleLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, SpectreConsoleLogger>
        _loggers = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _writeLock = new();
    private readonly IShell _shell;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreConsoleLoggerProvider"/> class with the specified shell.
    /// </summary>
    /// <param name="shell">The shell used to access the console and theme for logging output.</param>
    public SpectreConsoleLoggerProvider(IShell shell)
    {
        _shell = shell;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new SpectreConsoleLogger(name, this));

    /// <inheritdoc />
    public void Dispose() => _loggers.Clear();

    internal void WriteLogEntry(LogLevel logLevel, string category, string message, Exception? exception)
    {
        var (label, style) = GetLevelInfo(logLevel);

        lock (_writeLock)
        {
            var console = _shell.ErrorConsole;
            console.Write(new NoWrapText().Append(label, style).Append($": {category}: {message}"));

            if (exception is not null)
            {
                console.WriteException(exception, new ExceptionSettings
                {
                    Format = Spectre.Console.ExceptionFormats.ShortenEverything,
                    Style = _shell.Theme.ExceptionStyle
                });
            }
        }
    }

    private (string Label, Style Style) GetLevelInfo(LogLevel logLevel)
    {
        var label = logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "????"
        };

        // Justification: _shell.Console isn't thread-safe but Theme is.
        // ReSharper disable once InconsistentlySynchronizedField
        return (label, _shell.Theme.GetLogLevelStyle(logLevel));
    }
}