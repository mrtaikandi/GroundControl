#pragma warning disable CA1708
using System.Text.Json;
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides extension methods for <see cref="IShell"/> for rendering data as tables or JSON.
/// </summary>
public static partial class ShellExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    extension(IShell shell)
    {
        /// <summary>
        /// Renders a collection of items as either a Spectre.Console table or JSON, depending on the specified output format.
        /// </summary>
        /// <typeparam name="T">The type of items to render.</typeparam>
        /// <param name="items">The items to render.</param>
        /// <param name="headers">The column headers for table output.</param>
        /// <param name="valueExtractors">Functions that extract a display value from each item, one per column.</param>
        /// <param name="outputFormat">The output format to use.</param>
        public void RenderTable<T>(
            IReadOnlyList<T> items,
            IReadOnlyList<string> headers,
            IReadOnlyList<Func<T, string>> valueExtractors,
            OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Json)
            {
                shell.RenderJson(items);
                return;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);

            foreach (var header in headers)
            {
                table.AddColumn(new TableColumn(header).NoWrap());
            }

            foreach (var item in items)
            {
                var cells = new string[valueExtractors.Count];
                for (var i = 0; i < valueExtractors.Count; i++)
                {
                    cells[i] = Markup.Escape(valueExtractors[i](item));
                }

                table.AddRow(cells);
            }

            shell.Console.Write(table);
        }

        /// <summary>
        /// Renders a set of key-value pairs as either a vertical Spectre.Console table or JSON, depending on the specified output format.
        /// </summary>
        /// <param name="keyValuePairs">The key-value pairs to render.</param>
        /// <param name="outputFormat">The output format to use.</param>
        public void RenderDetail(
            IReadOnlyList<(string Key, string Value)> keyValuePairs,
            OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Json)
            {
                var dict = new Dictionary<string, string>(keyValuePairs.Count);
                foreach (var (key, value) in keyValuePairs)
                {
                    dict[key] = value;
                }

                shell.RenderJson(dict);
                return;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.HideHeaders();
            table.AddColumn(new TableColumn("Key").NoWrap());
            table.AddColumn(new TableColumn("Value"));

            foreach (var (key, value) in keyValuePairs)
            {
                table.AddRow($"[bold]{Markup.Escape(key)}[/]", Markup.Escape(value));
            }

            shell.Console.Write(table);
        }

        /// <summary>
        /// Renders an object as pretty-printed JSON to the console.
        /// </summary>
        /// <param name="value">The object to serialize and render.</param>
        public void RenderJson(object value)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            shell.Console.WriteLine(json);
        }
    }
}