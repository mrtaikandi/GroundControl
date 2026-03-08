using System.Globalization;

namespace GroundControl.Api.Shared;

internal static class EntityTagHeaders
{
    public static string Format(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

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