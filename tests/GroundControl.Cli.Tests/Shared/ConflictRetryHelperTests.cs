using GroundControl.Cli.Shared.ErrorHandling;
using GroundControl.Host.Cli;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Tests.Shared;

// Interactive confirmation path (noInteractive: false) cannot be tested with MockShellBuilder
// because Spectre.Console's ConfirmAsync requires real console input. The non-interactive path
// covers diff rendering, version display, and error messaging. The interactive confirm+retry
// path will be covered by integration tests when a testable IShell abstraction is available.
public sealed class ConflictRetryHelperTests
{
    [Fact]
    public async Task HandleConflictAsync_NonInteractive_ShowsDiffAndFails()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();
        var retryCalled = false;

        var diffs = new List<FieldDiff>
        {
            new("Name", "Old Name", "Current Name"),
            new("Description", "Old Desc", "Current Desc")
        };

        var conflictInfo = new ConflictInfo(42, diffs);

        // Act
        var result = await shell.HandleConflictAsync(
            _ => Task.FromResult(conflictInfo),
            (_, _) =>
            {
                retryCalled = true;
                return Task.CompletedTask;
            },
            noInteractive: true,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
        retryCalled.ShouldBeFalse();
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Version conflict");
        output.ShouldContain("Name");
        output.ShouldContain("Old Name");
        output.ShouldContain("Current Name");
        output.ShouldContain("Description");
        output.ShouldContain("Old Desc");
        output.ShouldContain("Current Desc");
        output.ShouldContain("42");
        output.ShouldContain("Re-run the command");
    }

    [Fact]
    public async Task HandleConflictAsync_NonInteractive_ShowsFieldDiffTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var diffs = new List<FieldDiff>
        {
            new("Key", "old-key", "new-key")
        };

        var conflictInfo = new ConflictInfo(5, diffs);

        // Act
        await shell.HandleConflictAsync(
            _ => Task.FromResult(conflictInfo),
            (_, _) => Task.CompletedTask,
            noInteractive: true,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Field");
        output.ShouldContain("Your Value");
        output.ShouldContain("Current Value");
        output.ShouldContain("old-key");
        output.ShouldContain("new-key");
    }

    [Fact]
    public async Task HandleConflictAsync_NoDiffs_ShowsNoDifferencesMessage()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var conflictInfo = new ConflictInfo(10, []);

        // Act
        await shell.HandleConflictAsync(
            _ => Task.FromResult(conflictInfo),
            (_, _) => Task.CompletedTask,
            noInteractive: true,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("No field differences detected");
    }

    [Fact]
    public async Task HandleConflictAsync_NonInteractive_ShowsCurrentVersion()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var conflictInfo = new ConflictInfo(99, [new FieldDiff("F", "a", "b")]);

        // Act
        await shell.HandleConflictAsync(
            _ => Task.FromResult(conflictInfo),
            (_, _) => Task.CompletedTask,
            noInteractive: true,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("99");
    }
}