using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Shared.Validation;

/// <summary>
/// Represents the result of an asynchronous validation operation.
/// </summary>
internal abstract record ValidatorResult
{
    /// <summary>
    /// Gets a result indicating that validation succeeded.
    /// </summary>
    public static ValidatorResult Success { get; } = new SuccessResult();

    /// <summary>
    /// Creates a result representing a problem with a specific status code.
    /// </summary>
    /// <param name="detail">The problem detail message.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    public static ValidatorResult Problem(string detail, int statusCode) =>
        new ProblemResult(detail, statusCode);

    /// <summary>
    /// Creates a result representing validation errors that should return a 400 response.
    /// </summary>
    /// <param name="errors">The validation errors dictionary.</param>
    public static ValidatorResult ValidationProblem(IDictionary<string, string[]> errors) =>
        new ValidationProblemResult(errors);

    /// <summary>
    /// Creates a result representing validation errors that should return a 400 response.
    /// </summary>
    /// <param name="results">The validation results to convert to an errors dictionary.</param>
    public static ValidatorResult ValidationProblem(params IEnumerable<ValidationResult> results) =>
        new ValidationProblemResult(results);

    internal sealed record ProblemResult(string Detail, int StatusCode) : ValidatorResult;

    internal sealed record SuccessResult : ValidatorResult;

    internal sealed record ValidationProblemResult(IDictionary<string, string[]> Errors) : ValidatorResult
    {
        public ValidationProblemResult(IEnumerable<ValidationResult> results)
            : this(ToErrorsDictionary(results)) { }

        private static Dictionary<string, string[]> ToErrorsDictionary(IEnumerable<ValidationResult> results)
        {
            var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                var message = result.ErrorMessage ?? "Validation failed.";
                var memberNames = result.MemberNames.Where(m => !string.IsNullOrEmpty(m)).ToArray();
                var key = memberNames is { Length: > 0 } ? string.Join(".", memberNames) : "";

                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors[key] = messages;
                }

                messages.Add(message);
            }

            return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        }
    }
}