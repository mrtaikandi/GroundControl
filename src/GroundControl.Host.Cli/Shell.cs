using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Spectre Console implementation of <see cref="IShell"/> that provides interactive console UI
/// including styled output, selection prompts, text input, and progress/status indicators.
/// </summary>
internal sealed class Shell : IShell
{
    public Shell(IAnsiConsole ansiConsole, TextReader? input = null)
    {
        ArgumentNullException.ThrowIfNull(ansiConsole);

        Console = ansiConsole;
        Input = input ?? System.Console.In;
        Theme = new Theme(ansiConsole);
    }

    public IAnsiConsole Console { get; }

    public TextReader Input { get; }

    public Theme Theme { get; }
}