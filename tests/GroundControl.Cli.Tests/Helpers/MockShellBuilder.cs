using System.Text;
using GroundControl.Host.Cli;
using Spectre.Console;

namespace GroundControl.Cli.Tests.Helpers;

internal sealed class MockShellBuilder
{
    private readonly StringBuilder _outputBuffer = new();

    public IShell Build()
    {
        var writer = new StringWriter(_outputBuffer);

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Interactive = InteractionSupport.No,
            Ansi = AnsiSupport.No
        });

        return new Shell(console);
    }

    public string GetOutput() => _outputBuffer.ToString();
}