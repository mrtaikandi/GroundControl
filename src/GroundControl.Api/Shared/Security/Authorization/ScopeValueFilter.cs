using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Shared.Security.Authorization;

/// <summary>
/// Filters <see cref="ScopedValue"/> lists based on user grant conditions.
/// </summary>
internal static class ScopeValueFilter
{
    /// <summary>
    /// Returns only the scoped values that the user is allowed to access based on their grant conditions.
    /// </summary>
    /// <param name="values">The scoped values to filter.</param>
    /// <param name="grants">The user's applicable grants (already filtered to the relevant resource scope).</param>
    /// <returns>The subset of scoped values permitted by the grants.</returns>
    public static IReadOnlyList<ScopedValue> Filter(IEnumerable<ScopedValue> values, IEnumerable<Grant> grants)
    {
        var grantList = grants as IReadOnlyList<Grant> ?? grants.ToList();

        // If any grant has null/empty conditions, user has unrestricted access
        if (grantList.Any(g => g.Conditions.Count == 0))
        {
            return values as IReadOnlyList<ScopedValue> ?? values.ToList();
        }

        return values.Where(v => IsAllowed(v, grantList)).ToList();
    }

    private static bool IsAllowed(ScopedValue value, IReadOnlyList<Grant> grants)
    {
        // Unscoped (default) values are always visible
        if (value.Scopes.Count == 0)
        {
            return true;
        }

        // A scope value is allowed if ANY grant's conditions match (union across grants)
        return grants.Any(g => ConditionsMatch(g.Conditions!, value.Scopes));
    }

    /// <summary>
    /// Checks whether a grant's conditions match a scope value's dimensions.
    /// All condition keys must match (AND), and any condition value within a key satisfies it (OR).
    /// </summary>
    private static bool ConditionsMatch(Dictionary<string, List<string>> conditions, Dictionary<string, string> scopes)
    {
        foreach (var (dimensionKey, allowedValues) in conditions)
        {
            // If the scope value doesn't have this dimension, the condition doesn't restrict it
            if (!scopes.TryGetValue(dimensionKey, out var scopeValue))
            {
                continue;
            }

            // The scope value must match one of the allowed values (OR within a key)
            if (!allowedValues.Contains(scopeValue, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}