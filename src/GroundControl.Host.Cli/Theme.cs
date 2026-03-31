using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace GroundControl.Host.Cli;

internal sealed class Theme
{
    private Style? _logCritical;
    private Style? _logDebug;
    private Style? _logError;
    private Style? _logInformation;
    private Style? _logTrace;
    private Style? _logWarning;

    public Theme(IAnsiConsole console)
    {
        SupportsColor = console.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
        Highlight = SupportsColor
            ? new Style(Color.Purple)
            : new Style(decoration: Decoration.Invert);

        _logTrace = SupportsColor ? new Style(decoration: Decoration.Dim) : Style.Plain;
        _logDebug = SupportsColor ? new Style(decoration: Decoration.Dim) : Style.Plain;
        _logInformation = SupportsColor ? new Style(Color.Blue) : Style.Plain;
        _logWarning = SupportsColor ? new Style(Color.Yellow) : Style.Plain;
        _logError = SupportsColor ? new Style(Color.Red, decoration: Decoration.Bold) : Style.Plain;
        _logCritical = SupportsColor ? new Style(Color.White, Color.Red) : Style.Plain;
    }

    public Style Highlight { get; }

    private bool SupportsColor { get; }

    public Style GetLogLevelStyle(LogLevel level) => level switch
    {
        LogLevel.Trace => _logTrace ??= CreateStyle(decoration: Decoration.Dim),
        LogLevel.Debug => _logDebug ??= CreateStyle(decoration: Decoration.Dim),
        LogLevel.Information => _logInformation ??= CreateStyle(Color.Blue),
        LogLevel.Warning => _logWarning ??= CreateStyle(Color.Yellow),
        LogLevel.Error => _logError ??= CreateStyle(Color.Red, decoration: Decoration.Bold),
        LogLevel.Critical => _logCritical ??= CreateStyle(Color.White, Color.Red),
        _ => Style.Plain
    };

    public ExceptionStyle ExceptionStyle => !SupportsColor
        ? new ExceptionStyle()
        : new ExceptionStyle
        {
            Exception = new Style(Color.Red, decoration: Decoration.Bold),
            Message = new Style(Color.Red),
            Method = new Style(Color.Yellow),
            ParameterType = new Style(Color.Blue),
            ParameterName = new Style(Color.Gray),
            Parenthesis = new Style(decoration: Decoration.Dim),
            Path = new Style(Color.Default),
            LineNumber = new Style(Color.Blue),
            Dimmed = new Style(decoration: Decoration.Dim),
            NonEmphasized = new Style(decoration: Decoration.Dim)
        };

    private Style CreateStyle(Color? foreground = null, Color? background = null, Decoration? decoration = null)
    {
        return !SupportsColor
            ? Style.Plain
            : new Style(foreground, background, decoration);
    }
}