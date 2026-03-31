#pragma warning disable CA1708
using Spectre.Console;

namespace GroundControl.Host.Cli;

/// <summary>
/// Provides extension methods for <see cref="IShell"/> for spinners and progress bars.
/// </summary>
public static partial class ShellExtensions
{
    extension(IShell shell)
    {
        /// <summary>
        /// Runs an action while displaying a spinner with status text.
        /// </summary>
        /// <param name="statusText">The text shown alongside the spinner.</param>
        /// <param name="action">The action to execute.</param>
        public void ShowStatus(string statusText, Action action)
        {
            shell.Console.Status()
                .Spinner(Spinner.Known.Dots3)
                .SpinnerStyle(shell.Theme.Highlight)
                .Start(statusText, _ => action());
        }

        /// <summary>
        /// Runs an async function while displaying a spinner with status text, returning a result.
        /// </summary>
        /// <typeparam name="T">The return type of the async function.</typeparam>
        /// <param name="statusText">The text shown alongside the spinner.</param>
        /// <param name="action">The async function to execute.</param>
        /// <returns>The result of <paramref name="action"/>.</returns>
        public async Task<T> ShowStatusAsync<T>(string statusText, Func<Task<T>> action)
        {
            return await shell.Console.Status()
                .Spinner(Spinner.Known.Dots3)
                .SpinnerStyle(shell.Theme.Highlight)
                .StartAsync(statusText, _ => action());
        }

        /// <summary>
        /// Runs an async action while displaying a spinner with status text.
        /// </summary>
        /// <param name="statusText">The text shown alongside the spinner.</param>
        /// <param name="action">The async action to execute.</param>
        /// <returns>A task that completes when <paramref name="action"/> finishes.</returns>
        public async Task ShowStatusAsync(string statusText, Func<Task> action)
        {
            await shell.Console.Status()
                .Spinner(Spinner.Known.Dots3)
                .SpinnerStyle(shell.Theme.Highlight)
                .StartAsync(statusText, _ => action());
        }

        /// <summary>
        /// Displays a progress bar while running an async action that can report progress.
        /// </summary>
        /// <param name="action">The async action that receives a <see cref="ProgressContext"/> for adding and updating progress tasks.</param>
        /// <returns>A task that completes when <paramref name="action"/> finishes.</returns>
        public async Task ShowProgressAsync(Func<ProgressContext, Task> action) =>
            await shell.Console.Progress().StartAsync(action);

        /// <summary>
        /// Displays a progress bar while running an action that can report progress.
        /// </summary>
        /// <param name="action">The action that receives a <see cref="ProgressContext"/> for adding and updating progress tasks.</param>
        public void ShowProgress(Action<ProgressContext> action) =>
            shell.Console.Progress().Start(action);
    }
}