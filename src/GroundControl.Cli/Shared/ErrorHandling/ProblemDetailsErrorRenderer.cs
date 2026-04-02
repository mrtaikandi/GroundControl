#pragma warning disable CA1708
using Spectre.Console;

namespace GroundControl.Cli.Shared.ErrorHandling;

internal static class ProblemDetailsErrorRenderer
{
    extension(IShell shell)
    {
        internal void RenderProblemDetails(ProblemDetailsApiException ex)
        {
            switch (ex.StatusCode)
            {
                case 400:
                    RenderValidationErrors(shell, ex);
                    break;
                case 404:
                    shell.DisplayError(ex.Detail ?? "The requested resource was not found.");
                    break;
                case 409:
                    shell.DisplayError(ex.Detail ?? "A conflict occurred. The resource may have been modified by another user.");
                    break;
                case 422:
                    shell.DisplayError(ex.Detail ?? "The request could not be processed.");
                    break;
                case 428:
                    shell.DisplayError("Version required — use the --version flag to specify the expected version.");
                    break;
                default:
                    if (ex.StatusCode >= 500)
                    {
                        shell.DisplayError("A server error occurred. Use --debug for details.");
                    }
                    else
                    {
                        shell.DisplayError(ex.Detail ?? ex.Title ?? $"Request failed with status {ex.StatusCode}.");
                    }

                    break;
            }
        }
    }

    private static void RenderValidationErrors(IShell shell, ProblemDetailsApiException ex)
    {
        shell.DisplayError(ex.Detail ?? "Validation failed.");

        if (ex.ValidationErrors.Count == 0)
        {
            return;
        }

        foreach (var (field, errors) in ex.ValidationErrors)
        {
            foreach (var error in errors)
            {
                var message = string.IsNullOrEmpty(field)
                    ? error
                    : $"{field}: {error}";

                shell.Console.MarkupLine($"  [red]{Markup.Escape(message)}[/]");
            }
        }
    }
}