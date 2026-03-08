using System.Globalization;
using System.Text.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

internal static class ConfigEntryValidation
{
    private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String",
        "Integer",
        "Boolean",
        "Json",
        "DateTime",
        "DateTimeOffset",
        "TimeOnly",
        "DateOnly"
    };

    public static bool IsValidValueType(string valueType) => AllowedValueTypes.Contains(valueType);

    public static string? ValidateValue(string value, string valueType)
    {
        return valueType.ToUpperInvariant() switch
        {
            "STRING" => null,
            "INTEGER" => long.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid Integer.",
            "BOOLEAN" => bool.TryParse(value, out _) ? null : $"Value '{value}' is not a valid Boolean.",
            "JSON" => ValidateJson(value),
            "DATETIME" => DateTime.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateTime.",
            "DATETIMEOFFSET" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateTimeOffset.",
            "TIMEONLY" => TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid TimeOnly.",
            "DATEONLY" => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _) ? null : $"Value '{value}' is not a valid DateOnly.",
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

    private static string? ValidateJson(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            return null;
        }
        catch (JsonException)
        {
            return $"Value '{value}' is not valid JSON.";
        }
    }
}