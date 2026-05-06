using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Resolvers;

/// <summary>
/// Resolves the most specific <see cref="ScopedValue" /> that matches a client's scope dimensions.
/// </summary>
internal sealed class ScopeResolver : IScopeResolver
{
    private readonly ILogger<ScopeResolver> _logger;

    public ScopeResolver(ILogger<ScopeResolver> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ScopedValue? Resolve(IReadOnlyList<ScopedValue> scopedValues, IReadOnlyDictionary<string, string> clientScopes)
    {
        ScopedValue? unscopedDefault = null;
        ScopedValue? bestMatch = null;
        var bestSpecificity = 0;
        var tieDetected = false;

        foreach (var candidate in scopedValues)
        {
            if (candidate.Scopes.Count == 0)
            {
                unscopedDefault = candidate;
                continue;
            }

            if (!IsFullMatch(candidate.Scopes, clientScopes))
            {
                continue;
            }

            var specificity = candidate.Scopes.Count;
            if (specificity > bestSpecificity)
            {
                bestMatch = candidate;
                bestSpecificity = specificity;
                tieDetected = false;
            }
            else if (specificity == bestSpecificity)
            {
                tieDetected = true;
            }
        }

        if (bestMatch is null)
        {
            return unscopedDefault;
        }

        if (tieDetected)
        {
            _logger.LogMultipleScopesWarning(bestSpecificity);
        }

        return bestMatch;
    }

    private static bool IsFullMatch(Dictionary<string, string> candidateScopes, IReadOnlyDictionary<string, string> clientScopes)
    {
        foreach (var scope in candidateScopes)
        {
            if (!TryGetClientValue(clientScopes, scope.Key, out var clientValue) || !string.Equals(clientValue, scope.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Case-insensitive lookup of a scope dimension. Scope dimensions are stored case-insensitively
    /// (the validator looks them up via the unique-by-dimension index with case-insensitive collation),
    /// so resolution must match that contract regardless of the casing the producer happened to use.
    /// </summary>
    private static bool TryGetClientValue(IReadOnlyDictionary<string, string> clientScopes, string dimension, out string? value)
    {
        if (clientScopes.TryGetValue(dimension, out value))
        {
            return true;
        }

        foreach (var entry in clientScopes)
        {
            if (string.Equals(entry.Key, dimension, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}

internal static partial class ScopeResolverLogs
{
    [LoggerMessage(1, LogLevel.Warning, "Multiple scoped values matched with the same specificity ({Specificity}). Returning the first candidate.")]
    public static partial void LogMultipleScopesWarning(this ILogger<ScopeResolver> logger, int specificity);
}