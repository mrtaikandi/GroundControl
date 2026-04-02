#pragma warning disable CA1708
using Spectre.Console;

namespace GroundControl.Cli.Shared.Pagination;

internal static class PaginatedRenderer
{
    internal readonly record struct Page<T>(IReadOnlyList<T> Items, string? NextCursor);

    extension(IShell shell)
    {
        internal async Task RenderPaginatedTableAsync<T>(
            Func<string?, CancellationToken, Task<Page<T>>> fetchPage,
            IReadOnlyList<string> headers,
            IReadOnlyList<Func<T, string>> valueExtractors,
            OutputFormat outputFormat,
            CancellationToken cancellationToken = default)
        {
            if (outputFormat == OutputFormat.Json)
            {
                var allItems = await CollectAllPagesAsync(fetchPage, cancellationToken).ConfigureAwait(false);
                shell.RenderJson(allItems);
                return;
            }

            await FetchAllPagesAsTableAsync(shell, fetchPage, headers, valueExtractors, cancellationToken).ConfigureAwait(false);
        }
    }

    // Spectre.Console's Table widget requires all rows before rendering, so pages are
    // collected into a single table rather than streamed incrementally to the console.
    private static async Task FetchAllPagesAsTableAsync<T>(
        IShell shell,
        Func<string?, CancellationToken, Task<Page<T>>> fetchPage,
        IReadOnlyList<string> headers,
        IReadOnlyList<Func<T, string>> valueExtractors,
        CancellationToken cancellationToken)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);

        foreach (var header in headers)
        {
            table.AddColumn(new TableColumn(header).NoWrap());
        }

        string? cursor = null;
        do
        {
            var page = await fetchPage(cursor, cancellationToken).ConfigureAwait(false);

            foreach (var item in page.Items)
            {
                var cells = new string[valueExtractors.Count];
                for (var i = 0; i < valueExtractors.Count; i++)
                {
                    cells[i] = Markup.Escape(valueExtractors[i](item));
                }

                table.AddRow(cells);
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);

        shell.Console.Write(table);
    }

    private static async Task<List<T>> CollectAllPagesAsync<T>(
        Func<string?, CancellationToken, Task<Page<T>>> fetchPage,
        CancellationToken cancellationToken)
    {
        var allItems = new List<T>();
        string? cursor = null;

        do
        {
            var page = await fetchPage(cursor, cancellationToken).ConfigureAwait(false);
            allItems.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return allItems;
    }
}