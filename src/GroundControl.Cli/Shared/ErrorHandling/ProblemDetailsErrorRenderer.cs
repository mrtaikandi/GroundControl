using GroundControl.Api.Client.Contracts;
using Spectre.Console;

namespace GroundControl.Cli.Shared.ErrorHandling;

internal static class ProblemDetailsErrorRenderer
{
    extension(IShell shell)
    {
        internal void RenderProblemDetails(ProblemDetails ex)
        {
            if (ex is HttpValidationProblemDetails validationProblemDetails)
            {
                shell.RenderValidationErrors(validationProblemDetails);
                return;
            }

            switch (ex.Status)
            {
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
                    if (ex.Status >= 500)
                    {
                        shell.DisplayError("A server error occurred. Use --debug for details.");
                    }
                    else
                    {
                        shell.DisplayError(ex.Detail ?? ex.Title ?? $"Request failed with status {ex.Status}.");
                    }

                    break;
            }
        }

        private void RenderValidationErrors(HttpValidationProblemDetails problem)
        {
            shell.DisplayError(problem.Detail ?? "Validation failed.");

            if (problem.Errors?.Count is null or 0)
            {
                return;
            }

            foreach (var (field, errors) in problem.Errors)
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
}