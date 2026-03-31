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
            shell.DisplayMessage("thumbs_down", $"[red bold]{errorMessage}[/]");

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
            shell.DisplayMessage("thumbs_up", message);
    }
}