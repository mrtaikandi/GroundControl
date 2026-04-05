using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides access to the underlying console for CLI applications, allowing for advanced console interactions and output formatting.
/// </summary>
public interface IShell
{
    /// <summary>
    /// Gets the underlying Spectre Console instance for standard output.
    /// </summary>
    IAnsiConsole Console { get; }

    /// <summary>
    /// Gets the Spectre Console instance for standard error output, used for diagnostics and logging.
    /// </summary>
    IAnsiConsole ErrorConsole { get; }

    internal TextReader Input { get; }

    internal Theme Theme { get; }
}