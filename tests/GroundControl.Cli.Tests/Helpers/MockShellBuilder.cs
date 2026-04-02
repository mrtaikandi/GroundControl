using System.Text;
using GroundControl.Host.Cli;
using Spectre.Console;

namespace GroundControl.Cli.Tests.Helpers;

internal sealed class MockShellBuilder
{
    private readonly StringBuilder _outputBuffer = new();
    private string? _inputText;

    public MockShellBuilder WithInput(string input)
    {
        _inputText = input;
        return this;
    }

    public IShell Build()
    {
        var writer = new StringWriter(_outputBuffer);

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Interactive = InteractionSupport.No,
            Ansi = AnsiSupport.No
        });

        var input = _inputText is not null ? new StringReader(_inputText) : null;
        return new Shell(console, input);
    }

    public string GetOutput() => _outputBuffer.ToString();
}