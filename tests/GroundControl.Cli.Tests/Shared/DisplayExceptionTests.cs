using GroundControl.Host.Cli;

namespace GroundControl.Cli.Tests.Shared;

public sealed class DisplayExceptionTests
{
    [Fact]
    public void DisplayExceptionSummary_RendersTypeNameAndMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var exception = new InvalidOperationException("server URL is not configured");

        // Act
        shell.DisplayExceptionSummary(exception);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("InvalidOperationException");
        output.ShouldContain("server URL is not configured");
    }

    [Fact]
    public void DisplayExceptionSummary_EscapesMessagesContainingMarkupCharacters()
    {
        // Arrange — Spectre.Console treats [ and ] as markup; an unescaped message would either crash
        // or produce mangled output. The summary must escape user-controlled exception text.
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var exception = new InvalidOperationException("expected token but got [unexpected]");

        // Act
        shell.DisplayExceptionSummary(exception);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("[unexpected]");
    }
}
