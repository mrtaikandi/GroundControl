#pragma warning disable SA1202, CA1708
using GroundControl.Host.Cli.Extensions.Spectre;
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides extension methods for <see cref="IShell"/> for user input and selection prompts.
/// </summary>
public static partial class ShellExtensions
{
    extension(IShell shell)
    {
        /// <summary>
        /// Prompts the user for a yes/no confirmation.
        /// </summary>
        /// <param name="promptText">The question to display.</param>
        /// <param name="defaultValue">The default answer when the user presses Enter without typing.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns><see langword="true"/> if the user confirmed; otherwise, <see langword="false"/>.</returns>
        public Task<bool> ConfirmAsync(
            string promptText,
            bool defaultValue = true,
            CancellationToken cancellationToken = default) =>
            shell.Console.ConfirmAsync(promptText, defaultValue, cancellationToken);

        /// <summary>
        /// Prompts the user to select a single item from a list of strings.
        /// </summary>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="enableSearch">When <see langword="true"/>, allows the user to type to filter choices.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The selected string.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public Task<string> PromptForSelectionAsync(
            string promptText,
            IReadOnlyCollection<string> choices,
            bool enableSearch = false,
            CancellationToken cancellationToken = default) =>
            shell.PromptForSelectionAsync(promptText, choices, x => x, enableSearch, cancellationToken);

        /// <summary>
        /// Prompts the user to select a single item from a list, using a custom formatter for display.
        /// </summary>
        /// <typeparam name="T">The type of items to choose from.</typeparam>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="choiceFormatter">A function that converts each choice to its display string.</param>
        /// <param name="enableSearch">When <see langword="true"/>, allows the user to type to filter choices.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The selected item.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public async Task<T> PromptForSelectionAsync<T>(
            string promptText,
            IReadOnlyCollection<T> choices,
            Func<T, string> choiceFormatter,
            bool enableSearch = false,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(promptText);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentNullException.ThrowIfNull(choiceFormatter);

            if (choices.Count == 0)
            {
                throw new EmptyChoicesException($"No items available for selection: {promptText}");
            }

            var prompt = new SelectionPrompt<T>()
                .Title(promptText)
                .UseConverter(choiceFormatter)
                .AddChoices(choices)
                .PageSize(10)
                .HighlightStyle(shell.Theme.Highlight);

            if (enableSearch)
            {
                prompt.EnableSearch();
            }

            var promptResult = await shell.Console.PromptAsync(prompt, cancellationToken);
            shell.EchoSelectedPrompt(promptText, promptResult.ToString() ?? string.Empty);

            return promptResult;
        }

        /// <summary>
        /// Prompts the user to select multiple items from a list.
        /// </summary>
        /// <typeparam name="T">The type of items to choose from.</typeparam>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="choiceFormatter">A function that converts each choice to its display string.</param>
        /// <param name="selectedChoices">Items that are pre-selected by default.</param>
        /// <param name="allowCustomOption">When <see langword="true"/> and <typeparamref name="T"/> is <see langword="string"/>,
        /// appends a "Custom value..." option that lets the user enter arbitrary values.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The list of selected items, including any custom values entered by the user.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public async Task<List<T>> PromptForMultiSelectionAsync<T>(
            string promptText,
            IReadOnlyCollection<T> choices,
            Func<T, string> choiceFormatter,
            IEnumerable<T> selectedChoices,
            bool allowCustomOption = false,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(promptText);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentNullException.ThrowIfNull(choiceFormatter);

            if (choices.Count == 0)
            {
                throw new EmptyChoicesException($"No items available for selection: {promptText}");
            }

            var prompt = new MultiSelectionPrompt<T>()
                .Title(promptText)
                .UseConverter(choiceFormatter)
                .AddChoices(choices)
                .PageSize(10)
                .HighlightStyle(shell.Theme.Highlight);

            if (allowCustomOption && typeof(T) == typeof(string))
            {
                prompt.AddChoice((T)(object)"Custom value...");
            }

            foreach (var selectedChoice in selectedChoices)
            {
                prompt.Select(selectedChoice);
            }

            var promptResult = await shell.Console.PromptAsync(prompt, cancellationToken);
            if (allowCustomOption && promptResult.Contains((T)(object)"Custom value..."))
            {
                promptResult.Remove((T)(object)"Custom value...");

                while (true)
                {
                    var customValue = await shell.PromptForStringAsync(
                        "Enter the [green]custom value[/] ([green]#[/] to finish):",
                        cancellationToken: cancellationToken);

                    if (customValue == "#")
                    {
                        break;
                    }

                    promptResult.Add((T)(object)customValue);
                }
            }

            shell.EchoSelectedPrompt(promptText, string.Join(", ", promptResult));
            return promptResult;
        }

        /// <summary>
        /// Prompts the user to enter a string value with optional default, validation, and optional input.
        /// </summary>
        /// <param name="promptText">The prompt text to display.</param>
        /// <param name="defaultValue">An optional default value shown to the user.</param>
        /// <param name="validator">An optional validation function that returns a <see cref="ValidationResult"/>.</param>
        /// <param name="isOptional">When <see langword="true"/>, allows the user to submit an empty value.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The entered string value.</returns>
        public async Task<string> PromptForStringAsync(
            string promptText,
            string? defaultValue = null,
            Func<string, ValidationResult>? validator = null,
            bool isOptional = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(promptText);
            var prompt = new TextPrompt<string>(promptText);

            if (defaultValue is not null)
            {
                prompt.DefaultValue(defaultValue);
                prompt.ShowDefaultValue();
            }

            if (validator is not null)
            {
                prompt.Validate(validator);
            }

            if (isOptional)
            {
                prompt.AllowEmpty();
            }

            return await shell.Console.PromptAsync(prompt, cancellationToken);
        }

        /// <summary>
        /// Synchronously prompts the user to select a single item from a list of strings.
        /// </summary>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="enableSearch">When <see langword="true"/>, allows the user to type to filter choices.</param>
        /// <returns>The selected string.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public string PromptForSelection(
            string promptText,
            IReadOnlyCollection<string> choices,
            bool enableSearch = false) =>
            shell.PromptForSelection(promptText, choices, x => x, enableSearch);

        /// <summary>
        /// Synchronously prompts the user to select a single item from a list, using a custom formatter for display.
        /// </summary>
        /// <typeparam name="T">The type of items to choose from.</typeparam>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="choiceFormatter">A function that converts each choice to its display string.</param>
        /// <param name="enableSearch">When <see langword="true"/>, allows the user to type to filter choices.</param>
        /// <returns>The selected item.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public T PromptForSelection<T>(
            string promptText,
            IReadOnlyCollection<T> choices,
            Func<T, string> choiceFormatter,
            bool enableSearch = false)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(promptText);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentNullException.ThrowIfNull(choiceFormatter);

            if (choices.Count == 0)
            {
                throw new EmptyChoicesException($"No items available for selection: {promptText}");
            }

            var prompt = new SelectionPrompt<T>()
                .Title(promptText)
                .UseConverter(choiceFormatter)
                .AddChoices(choices)
                .PageSize(10)
                .HighlightStyle(shell.Theme.Highlight);

            if (enableSearch)
            {
                prompt.EnableSearch();
            }

            var promptResult = shell.Console.Prompt(prompt);
            shell.EchoSelectedPrompt(promptText, promptResult.ToString() ?? string.Empty);

            return promptResult;
        }

        /// <summary>
        /// Synchronously prompts the user to select multiple items from a list.
        /// </summary>
        /// <typeparam name="T">The type of items to choose from.</typeparam>
        /// <param name="promptText">The prompt title.</param>
        /// <param name="choices">The available choices.</param>
        /// <param name="choiceFormatter">A function that converts each choice to its display string.</param>
        /// <param name="selectedChoices">Items that are pre-selected by default.</param>
        /// <param name="allowCustomOption">When <see langword="true"/> and <typeparamref name="T"/> is <see langword="string"/>,
        /// appends a "Custom value..." option that lets the user enter arbitrary values.</param>
        /// <returns>The collection of selected items, including any custom values entered by the user.</returns>
        /// <exception cref="EmptyChoicesException">Thrown when <paramref name="choices"/> is empty.</exception>
        public IReadOnlyCollection<T> PromptForMultiSelection<T>(
            string promptText,
            IReadOnlyCollection<T> choices,
            Func<T, string> choiceFormatter,
            IReadOnlyCollection<T> selectedChoices,
            bool allowCustomOption = false)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(promptText);
            ArgumentNullException.ThrowIfNull(choices);
            ArgumentNullException.ThrowIfNull(choiceFormatter);

            if (choices.Count == 0)
            {
                throw new EmptyChoicesException($"No items available for selection: {promptText}");
            }

            var prompt = new MultiSelectionPrompt<T>()
                .Title(promptText)
                .UseConverter(choiceFormatter)
                .AddChoices(choices)
                .PageSize(10)
                .HighlightStyle(shell.Theme.Highlight);

            if (allowCustomOption && typeof(T) == typeof(string))
            {
                prompt.AddChoice((T)(object)"Custom value...");
            }

            foreach (var selectedChoice in selectedChoices)
            {
                prompt.Select(selectedChoice);
            }

            var promptResult = shell.Console.Prompt(prompt);
            if (allowCustomOption && promptResult.Contains((T)(object)"Custom value..."))
            {
                promptResult.Remove((T)(object)"Custom value...");

                while (true)
                {
                    var customValue = shell.PromptForString(
                        "Enter the [green]custom value[/] ([green]#[/] to finish):");

                    if (customValue == "#")
                    {
                        break;
                    }

                    promptResult.Add((T)(object)customValue);
                }
            }

            shell.EchoSelectedPrompt(promptText, string.Join(", ", promptResult));

            return promptResult;
        }

        /// <summary>
        /// Synchronously prompts the user to enter a string value with optional default, validation, and optional input.
        /// </summary>
        /// <param name="promptText">The prompt text to display.</param>
        /// <param name="defaultValue">An optional default value shown to the user.</param>
        /// <param name="validator">An optional validation function that returns a <see cref="ValidationResult"/>.</param>
        /// <param name="isOptional">When <see langword="true"/>, allows the user to submit an empty value.</param>
        /// <returns>The entered string value.</returns>
        public string PromptForString(
            string promptText,
            string? defaultValue = null,
            Func<string, ValidationResult>? validator = null,
            bool isOptional = false)
        {
            ArgumentNullException.ThrowIfNull(promptText);
            var prompt = new TextPrompt<string>(promptText);

            if (defaultValue is not null)
            {
                prompt.DefaultValue(defaultValue);
                prompt.ShowDefaultValue();
            }

            if (validator is not null)
            {
                prompt.Validate(validator);
            }

            if (isOptional)
            {
                prompt.AllowEmpty();
            }

            return shell.Console.Prompt(prompt);
        }

        /// <summary>
        /// Reads lines from the shell input until an empty line or end-of-stream is encountered.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The concatenated text of all lines read, or <see langword="null"/> if no non-empty input was received.</returns>
        public async Task<string?> ReadLinesAsync(CancellationToken cancellationToken = default)
        {
            var sb = new System.Text.StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await shell.Input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                sb.AppendLine(line);
            }

            var result = sb.ToString().Trim();
            return result.Length == 0 ? null : result;
        }

        private void EchoSelectedPrompt(string promptText, string selection)
        {
            var paragraph = new NoWrapText()
                .Append(promptText + " ", Style.Plain)
                .Append(selection, shell.Theme.Highlight);

            shell.Console.Write(paragraph);
        }
    }
}