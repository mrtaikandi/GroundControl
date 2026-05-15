using System.Globalization;
using System.Text.RegularExpressions;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

/// <summary>
/// Shared validation and canonicalization logic for config entry write paths.
/// </summary>
internal sealed partial class ConfigEntryValidation
{
    /// <summary>
    /// Allowed shape for a config entry key: starts with a letter, then any mix of letters,
    /// digits, and the separators <c>.</c>, <c>:</c>, <c>_</c>, <c>-</c>.
    /// </summary>
    public const string KeyPattern = "^[A-Za-z][A-Za-z0-9.:_-]*$";

    /// <summary>
    /// Human-readable description of <see cref="KeyPattern" />, surfaced verbatim in 400 responses.
    /// </summary>
    public const string KeyPatternErrorMessage = "Key must start with a letter and contain only letters, digits, '.', ':', '_', or '-'.";

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

    private readonly IScopeStore _scopeStore;

    public ConfigEntryValidation(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    [GeneratedRegex(KeyPattern, RegexOptions.Compiled)]
    private static partial Regex KeyRegex { get; }

    public bool IsValidKey(string key) => !string.IsNullOrEmpty(key) && KeyRegex.IsMatch(key);

    public bool IsValidValueType(string valueType) => AllowedValueTypes.Contains(valueType);

    /// <summary>
    /// Validates every scope reference (dimension exists + value is in <see cref="Scope.AllowedValues" />)
    /// and rewrites each scope key to the canonical <see cref="Scope.Dimension" /> casing in a single
    /// pass. Each unique dimension is looked up once via <see cref="IScopeStore.GetByDimensionAsync" />.
    /// On failure, returns the first error; on success, returns the canonicalized list.
    /// </summary>
    public async Task<ScopeValidationResult> ValidateAndCanonicalizeScopesAsync(IReadOnlyList<ScopedValueRequest> values, CancellationToken cancellationToken)
    {
        var scopeCache = new Dictionary<string, Scope?>(StringComparer.OrdinalIgnoreCase);
        var canonical = new List<ScopedValueRequest>(values.Count);

        foreach (var scopedValue in values)
        {
            if (scopedValue.Scopes.Count == 0)
            {
                canonical.Add(scopedValue);
                continue;
            }

            var normalizedScopes = new Dictionary<string, string>(scopedValue.Scopes.Count);
            foreach (var (dimension, value) in scopedValue.Scopes)
            {
                if (!scopeCache.TryGetValue(dimension, out var scope))
                {
                    scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                    scopeCache[dimension] = scope;
                }

                if (scope is null)
                {
                    return ScopeValidationResult.Failure($"Scope dimension '{dimension}' does not exist.");
                }

                if (scope.AllowedValues.Count > 0 && !scope.AllowedValues.Contains(value))
                {
                    return ScopeValidationResult.Failure($"Value '{value}' is not allowed for scope dimension '{dimension}'.");
                }

                normalizedScopes[scope.Dimension] = value;
            }

            canonical.Add(scopedValue with { Scopes = normalizedScopes });
        }

        return ScopeValidationResult.Success(canonical);
    }

    public string? ValidateValue(string value, string valueType)
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
}

/// <summary>
/// Result of <see cref="ConfigEntryValidation.ValidateAndCanonicalizeScopesAsync" />: either a
/// validation error message, or a canonicalized list of scoped values.
/// </summary>
internal readonly record struct ScopeValidationResult
{
    private ScopeValidationResult(string? error, List<ScopedValueRequest>? canonical)
    {
        Error = error;
        Canonical = canonical;
    }

    /// <summary>Gets the canonicalized scoped values on success, or <c>null</c> on failure.</summary>
    public List<ScopedValueRequest>? Canonical { get; }

    /// <summary>Gets the first validation error, or <c>null</c> on success.</summary>
    public string? Error { get; }

    public static ScopeValidationResult Failure(string error) => new(error, null);

    public static ScopeValidationResult Success(List<ScopedValueRequest> canonical) => new(null, canonical);
}