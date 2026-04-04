using GroundControl.Api.Client.Contracts;
using Spectre.Console;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Shared.ErrorHandling;

internal static class ProblemDetailsErrorRenderer
{
    extension(IShell shell)
    {
        internal async Task<(int ExitCode, T? Result)> TryCallAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            try
            {
                var result = await action(cancellationToken);
                return (0, result);
            }
            catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return (1, default);
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return (1, default);
            }
        }

        internal async Task<int> TryCallAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            try
            {
                await action(cancellationToken);
                return 0;
            }
            catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return 1;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

        internal async Task<int> TryCallWithConflictHandlingAsync(
            bool noInteractive,
            Func<CancellationToken, Task<int>> action,
            Func<CancellationToken, Task<ConflictInfo>> fetchConflictInfo,
            Func<long, CancellationToken, Task> retryAction,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
            {
                var retried = await shell.HandleConflictAsync(
                    fetchConflictInfo,
                    retryAction,
                    noInteractive,
                    cancellationToken);

                return retried ? 0 : 1;
            }
            catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return 1;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

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