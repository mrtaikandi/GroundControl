#pragma warning disable SA1202, CA1708
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides extension methods for <see cref="IShell"/> for console output and formatting.
/// </summary>
public static partial class ShellExtensions
{
    extension(IShell shell)
    {
        /// <summary>
        /// Writes an empty line to the console.
        /// </summary>
        public void DisplayEmptyLine() => shell.Console.WriteLine();

        /// <summary>
        /// Displays an error message with a visual indicator.
        /// </summary>
        /// <param name="errorMessage">The error text to display.</param>
        public void DisplayError(string errorMessage) =>
            shell.DisplayMessage("cross_mark", $"[red bold]{errorMessage}[/]");

        /// <summary>
        /// Renders an exception to the console.
        /// </summary>
        /// <param name="ex">The exception to display.</param>
        /// <param name="exceptionFormats">Controls how much of the exception detail is shown.</param>
        public void DisplayException(
            Exception ex,
            ExceptionFormats exceptionFormats = ExceptionFormats.ShortenEverything) =>
            shell.Console.WriteException(ex, exceptionFormats.ToSpectreExceptionFormats());

        /// <summary>
        /// Renders a one-line summary of an exception in the form <c>TypeName: Message</c>.
        /// </summary>
        /// <param name="ex">The exception to summarize.</param>
        public void DisplayExceptionSummary(Exception ex)
        {
            var summary = $"{ex.GetType().Name}: {ex.Message}";
            shell.Console.MarkupLine($"   [red]{Markup.Escape(summary)}[/]");
        }

        /// <summary>
        /// Displays a sequence of output lines, coloring stderr lines in red.
        /// </summary>
        /// <param name="lines">Tuples of (stream name, line text) where stream is <c>"stdout"</c> or <c>"stderr"</c>.</param>
        public void DisplayLines(IEnumerable<(string Stream, string Line)> lines)
        {
            foreach (var (stream, line) in lines)
            {
                shell.Console.MarkupLineInterpolated(
                    stream == "stdout" ? (FormattableString)$"{line}" : (FormattableString)$"[red]{line}[/]");
            }
        }

        /// <summary>
        /// Displays a message prefixed with an emoji.
        /// </summary>
        /// <param name="emoji">The Spectre Console emoji shortcode (without colons).</param>
        /// <param name="message">The Spectre markup message to display.</param>
        public void DisplayMessage(string emoji, string message) =>
            shell.Console.MarkupLine($":{emoji}:  {message}");

        /// <summary>
        /// Displays a plain text message to the console.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void DisplayMessage(string message) =>
            shell.Console.Write(message);

        /// <summary>
        /// Displays a dimmed (subtle) message to the console.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void DisplaySubtleMessage(string message) =>
            shell.Console.MarkupLine($"[dim]{message}[/]");

        /// <summary>
        /// Displays a success message with a visual indicator.
        /// </summary>
        /// <param name="message">The success text to display.</param>
        public void DisplaySuccess(string message) =>
            shell.Console.MarkupLine($"[green]:check_mark:[/]  {message}");

        /// <summary>
        /// Displays a success result with format awareness. In <see cref="OutputFormat.Table"/> mode,
        /// displays the message produced by <paramref name="message"/> with a visual indicator.
        /// In <see cref="OutputFormat.Json"/> mode, serializes <paramref name="value"/> as JSON.
        /// </summary>
        /// <typeparam name="T">The type of the response value.</typeparam>
        /// <param name="value">The API response object to serialize in JSON mode.</param>
        /// <param name="outputFormat">The output format to use.</param>
        /// <param name="message">A function that builds the human-readable success message from the value.</param>
        public void DisplaySuccess<T>(T value, OutputFormat outputFormat, Func<T, string> message)
        {
            if (outputFormat == OutputFormat.Json)
            {
                shell.RenderJson(value!);
                return;
            }

            shell.DisplaySuccess(message(value));
        }

        /// <summary>
        /// Displays a success result for operations with no response body (e.g., delete).
        /// In <see cref="OutputFormat.Table"/> mode, displays the message with a visual indicator.
        /// In <see cref="OutputFormat.Json"/> mode, emits a structured status object.
        /// </summary>
        /// <param name="message">The success text to display.</param>
        /// <param name="outputFormat">The output format to use.</param>
        public void DisplaySuccess(string message, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Json)
            {
                shell.RenderJson(new { status = "success", message });
                return;
            }

            shell.DisplaySuccess(message);
        }
    }
}