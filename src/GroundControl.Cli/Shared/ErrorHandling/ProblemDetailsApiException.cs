namespace GroundControl.Cli.Shared.ErrorHandling;

internal sealed class ProblemDetailsApiException : Exception
{
    public ProblemDetailsApiException()
    {
    }

    public ProblemDetailsApiException(string message) : base(message)
    {
    }

    public ProblemDetailsApiException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ProblemDetailsApiException(
        int statusCode,
        string? title,
        string? detail,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(detail ?? title ?? $"HTTP {statusCode} error")
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public int StatusCode { get; }

    public string? Title { get; }

    public string? Detail { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; } = new Dictionary<string, string[]>();
}