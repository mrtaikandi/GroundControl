#pragma warning disable CA1708
using Spectre.Console;

namespace GroundControl.Cli.Shared.ErrorHandling;

internal static class ConflictRetryHelper
{
    internal readonly record struct FieldDiff(string Name, string OldValue, string CurrentValue);

    internal readonly record struct ConflictInfo(long CurrentVersion, IReadOnlyList<FieldDiff> Diffs);

    extension(IShell shell)
    {
        internal async Task<bool> HandleConflictAsync(
            Func<CancellationToken, Task<ConflictInfo>> fetchCurrentState,
            Func<long, CancellationToken, Task> retryOperation,
            bool noInteractive,
            CancellationToken cancellationToken = default)
        {
            shell.DisplayError("Version conflict — the resource has been modified.");

            var conflictInfo = await fetchCurrentState(cancellationToken).ConfigureAwait(false);
            RenderDiff(shell, conflictInfo.Diffs);

            if (noInteractive)
            {
                shell.Console.MarkupLine(
                    $"[yellow]Current version is {conflictInfo.CurrentVersion}. " +
                    "Re-run the command with the updated version to apply your changes.[/]");
                return false;
            }

            var confirmed = await shell.Console.ConfirmAsync(
                    "Apply your changes using the current version?", defaultValue: false, cancellationToken)
                .ConfigureAwait(false);

            if (!confirmed)
            {
                shell.Console.MarkupLine("[dim]Operation cancelled.[/]");
                return false;
            }

            await retryOperation(conflictInfo.CurrentVersion, cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    private static void RenderDiff(IShell shell, IReadOnlyList<FieldDiff> diffs)
    {
        if (diffs.Count == 0)
        {
            shell.Console.MarkupLine("[dim]No field differences detected.[/]");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("Field").NoWrap());
        table.AddColumn(new TableColumn("Your Value"));
        table.AddColumn(new TableColumn("Current Value"));

        foreach (var diff in diffs)
        {
            table.AddRow(
                Markup.Escape(diff.Name),
                $"[red]{Markup.Escape(diff.OldValue)}[/]",
                $"[green]{Markup.Escape(diff.CurrentValue)}[/]");
        }

        shell.Console.Write(table);
    }
}