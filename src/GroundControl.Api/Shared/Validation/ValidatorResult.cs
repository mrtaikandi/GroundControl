using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Shared.Validation;

/// <summary>
/// Represents the result of an asynchronous validation operation.
/// </summary>
internal sealed record ValidatorResult
{
    /// <summary>
    /// Gets a result indicating that validation succeeded.
    /// </summary>
    public static readonly ValidatorResult Success = new();

    private readonly string? _detail;
    private readonly int _statusCode;
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public ValidatorResult() { }

    private ValidatorResult(string? detail = null, int statusCode = 0)
    {
        _detail = detail;
        _statusCode = statusCode;
    }

    public bool IsSuccess => _errors.Count == 0 && _statusCode == 0;

    public bool IsFailed => !IsSuccess;

    /// <summary>
    /// Creates a result representing a problem with a specific status code.
    /// </summary>
    /// <param name="detail">The problem detail message.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    public static ValidatorResult Problem(string detail, int statusCode) => new(detail, statusCode);

    public static ValidatorResult Fail(string error, params string[] memberNames)
    {
        var result = new ValidatorResult();
        result.AddError(error, memberNames);

        return result;
    }

    public ValidatorResult AddError(string error, params string[] memberNames)
    {
        var key = memberNames.Length > 0 ? string.Join(".", memberNames) : string.Empty;
        if (!_errors.TryGetValue(key, out var messages))
        {
            messages = [];
            _errors[key] = messages;
        }

        messages.Add(error);

        return this;
    }

    public ProblemDetails? ToProblemDetails()
    {
        if (IsSuccess)
        {
            return null;
        }

        return _statusCode == 0
            ? new HttpValidationProblemDetails(_errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase))
            : new ProblemDetails { Detail = _detail, Status = _statusCode };
    }
}