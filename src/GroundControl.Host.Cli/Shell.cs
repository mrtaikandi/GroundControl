using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Spectre Console implementation of <see cref="IShell"/> that provides interactive console UI
/// including styled output, selection prompts, text input, and progress/status indicators.
/// </summary>
internal sealed class Shell : IShell
{
    public Shell(IAnsiConsole ansiConsole)
    {
        ArgumentNullException.ThrowIfNull(ansiConsole);

        Console = ansiConsole;
        Theme = new Theme(ansiConsole);
    }

    public IAnsiConsole Console { get; }

    public Theme Theme { get; }
}