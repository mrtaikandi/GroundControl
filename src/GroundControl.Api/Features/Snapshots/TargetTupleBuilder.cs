namespace GroundControl.Api.Features.Snapshots;

/// <summary>
/// Generates the dim-space cartesian set of target scope tuples that a config entry value must
/// be resolved against, based on the scope dimensions referenced by the entry's variables.
/// </summary>
/// <remarks>
/// Per-dimension domain is the union of distinct values referenced by the variables, plus an
/// "unspecified" axis (modeled as the dimension being absent from the tuple). The cartesian
/// product across these per-dimension domains yields the targets to materialize.
///
/// Variables that have only an unscoped default contribute no dimensions; an empty referenced-
/// variable list produces a single empty target tuple, matching the variable's runtime fallback
/// to its default.
/// </remarks>
internal static class TargetTupleBuilder
{
    /// <summary>
    /// Builds the target scope tuples for the given referenced variables.
    /// </summary>
    /// <param name="referencedVariables">Variables that the entry value's placeholders resolve to.</param>
    /// <returns>
    /// A canonically ordered list of target scope tuples. When no dimensions are referenced the
    /// list contains a single empty tuple.
    /// </returns>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Build(IReadOnlyCollection<PlaintextVariable> referencedVariables)
    {
        ArgumentNullException.ThrowIfNull(referencedVariables);

        var dimensionValues = CollectDimensionValues(referencedVariables);
        if (dimensionValues.Count == 0)
        {
            return [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)];
        }

        var (dimensions, domains) = BuildPerDimensionDomains(dimensionValues);
        return ExpandCartesian(dimensions, domains);
    }

    /// <summary>
    /// Collects the distinct values referenced per scope dimension across every scoped value of
    /// every input variable. Returned as <see cref="SortedDictionary{TKey, TValue}"/> with
    /// <see cref="SortedSet{T}"/> buckets so dimension and value ordering is canonical without an
    /// extra sort pass — load-bearing for diff-hash determinism.
    /// </summary>
    private static SortedDictionary<string, SortedSet<string>> CollectDimensionValues(IReadOnlyCollection<PlaintextVariable> referencedVariables)
    {
        var result = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in referencedVariables)
        {
            foreach (var scopedValue in variable.Values)
            {
                foreach (var (dimension, value) in scopedValue.Scopes)
                {
                    if (!result.TryGetValue(dimension, out var values))
                    {
                        result[dimension] = values = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    values.Add(value);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Materializes the per-dimension domain arrays. Each domain is prefixed with the
    /// "unspecified" sentinel (<see langword="null"/>) so the cartesian expansion includes the
    /// case where the dimension is absent from the resulting tuple.
    /// </summary>
    private static (string[] Dimensions, string?[][] Domains) BuildPerDimensionDomains(SortedDictionary<string, SortedSet<string>> dimensionValues)
    {
        var dimensions = new string[dimensionValues.Count];
        var domains = new string?[dimensionValues.Count][];
        var index = 0;

        foreach (var pair in dimensionValues)
        {
            dimensions[index] = pair.Key;

            var domain = new string?[pair.Value.Count + 1];
            domain[0] = null;
            var domainIndex = 1;
            foreach (var value in pair.Value)
            {
                domain[domainIndex++] = value;
            }

            domains[index] = domain;
            index++;
        }

        return (dimensions, domains);
    }

    private static List<IReadOnlyDictionary<string, string>> ExpandCartesian(string[] dimensions, string?[][] domains)
    {
        // Tracked as long so a pathological combination doesn't silently overflow the int capacity
        // hint. The 16MB BSON guard on the snapshot publisher catches runaway expansion long before
        // we'd ever reach int.MaxValue tuples; this is just to keep the capacity hint sane.
        var totalCount = domains.Aggregate<string?[], long>(1, (current, domain) => current * domain.Length);

        var capacity = totalCount is > 0 and <= int.MaxValue ? (int)totalCount : 0;
        var results = capacity > 0 ? new List<IReadOnlyDictionary<string, string>>(capacity) : [];
        var current = new string?[dimensions.Length];

        Recurse(0, dimensions, domains, current, results);
        return results;
    }

    private static void Recurse(int depth, string[] dimensions, string?[][] domains, string?[] current, List<IReadOnlyDictionary<string, string>> results)
    {
        if (depth == dimensions.Length)
        {
            var tuple = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < dimensions.Length; i++)
            {
                if (current[i] is { } value)
                {
                    tuple[dimensions[i]] = value;
                }
            }

            results.Add(tuple);
            return;
        }

        foreach (var value in domains[depth])
        {
            current[depth] = value;
            Recurse(depth + 1, dimensions, domains, current, results);
        }
    }
}