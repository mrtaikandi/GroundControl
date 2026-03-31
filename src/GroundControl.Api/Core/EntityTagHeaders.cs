using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using GroundControl.Api.Core.Validation;

namespace GroundControl.Api.Core;

internal static class EntityTagHeaders
{
    private const string IfMatchRequiredMessage = "If-Match header is required.";

    public static string Format(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static ValidatorResult ValidateIfMatch(HttpContext httpContext)
    {
        return TryParseIfMatch(httpContext, out _)
            ? ValidatorResult.Success
            : ValidatorResult.Problem(IfMatchRequiredMessage, StatusCodes.Status428PreconditionRequired);
    }

    public static bool TryParseIfMatch(
        HttpContext httpContext,
        out long expectedVersion,
        [NotNullWhen(false)] out IResult? problem)
    {
        if (TryParseIfMatch(httpContext, out expectedVersion))
        {
            problem = null;
            return true;
        }

        problem = TypedResults.Problem(detail: IfMatchRequiredMessage, statusCode: StatusCodes.Status428PreconditionRequired);
        return false;
    }

    public static bool TryParseIfMatch(HttpContext httpContext, out long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        expectedVersion = 0;

        var headerValues = httpContext.Request.Headers.IfMatch;
        if (headerValues.Count == 0)
        {
            return false;
        }

        var headerValue = headerValues[0];
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var normalizedValue = headerValue.Trim();
        if (normalizedValue.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedValue is ['"', _, ..] && normalizedValue[^1] == '"')
        {
            normalizedValue = normalizedValue[1..^1];
        }

        return long.TryParse(normalizedValue, NumberStyles.None, CultureInfo.InvariantCulture, out expectedVersion);
    }
}