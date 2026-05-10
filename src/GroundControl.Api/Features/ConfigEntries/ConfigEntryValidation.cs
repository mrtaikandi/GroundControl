using System.Globalization;
using System.Text.RegularExpressions;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

internal static partial class ConfigEntryValidation
{
    /// <summary>
    /// Allowed shape for a config entry key: starts with a letter, then any mix of letters,
    /// digits, and the separators <c>.</c>, <c>:</c>, <c>_</c>, <c>-</c>.
    /// </summary>
    public const string KeyPattern = "^[A-Za-z][A-Za-z0-9.:_-]*$";

    /// <summary>
    /// Human-readable description of <see cref="KeyPattern"/>, surfaced verbatim in 400 responses.
    /// </summary>
    public const string KeyPatternErrorMessage = "Key must start with a letter and contain only letters, digits, '.', ':', '_', or '-'.";

    [GeneratedRegex(KeyPattern, RegexOptions.Compiled)]
    private static partial Regex KeyRegex { get; }

    public static bool IsValidKey(string key) => !string.IsNullOrEmpty(key) && KeyRegex.IsMatch(key);

    private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String",
        "Int32",
        "Int64",
        "Double",
        "Decimal",
        "Boolean",
        "DateTime",
        "DateTimeOffset",
        "DateOnly",
        "TimeOnly"
    };

    public static bool IsValidValueType(string valueType) => AllowedValueTypes.Contains(valueType);

    public static string? ValidateValue(string value, string valueType)
    {
        return valueType.ToUpperInvariant() switch
        {
            "STRING" => null,
            "INT32" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid Int32.",
            "INT64" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid Int64.",
            "DOUBLE" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid Double.",
            "DECIMAL" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid Decimal.",
            "BOOLEAN" => bool.TryParse(value, out _) ? null : $"Value '{value}' is not a valid Boolean.",
            "DATETIME" => DateTime.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateTime.",
            "DATETIMEOFFSET" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateTimeOffset.",
            "DATEONLY" => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateOnly.",
            "TIMEONLY" => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid TimeOnly.",
            _ => $"ValueType '{valueType}' is not supported."
        };
    }

    public static async Task<string?> ValidateScopesAsync(
        IReadOnlyList<ScopedValueRequest> values,
        IScopeStore scopeStore,
        CancellationToken cancellationToken)
    {
        var dimensionCache = new Dictionary<string, Scope?>(StringComparer.OrdinalIgnoreCase);

        foreach (var scopedValue in values)
        {
            foreach (var (dimension, value) in scopedValue.Scopes)
            {
                if (!dimensionCache.TryGetValue(dimension, out var scope))
                {
                    scope = await scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                    dimensionCache[dimension] = scope;
                }

                if (scope is null)
                {
                    return $"Scope dimension '{dimension}' does not exist.";
                }

                if (scope.AllowedValues.Count > 0 && !scope.AllowedValues.Contains(value))
                {
                    return $"Value '{value}' is not allowed for scope dimension '{dimension}'.";
                }
            }
        }

        return null;
    }
}