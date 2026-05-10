using System.Collections.Frozen;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots;

/// <summary>
/// Fans out a single config entry into the resolved per-scope-tuple values that will be persisted
/// into a snapshot.
/// </summary>
/// <remarks>
/// For each source scoped value on the entry the builder:
/// <list type="number">
///     <item>Scans the value text for <c>{{variableName}}</c> placeholders and resolves each name
///     against the project then global variable lookup (preserving the existing two-tier
///     precedence).</item>
///     <item>Generates the dimension-space (dim-space) cartesian set of target scope tuples via
///     <see cref="TargetTupleBuilder"/> based on the scope dimensions referenced by the resolved
///     variables.</item>
///     <item>Merges each target with the source's own scope tuple (dropping conflicting
///     combinations) and produces a final scope tuple.</item>
///     <item>Calls <see cref="VariableInterpolator"/> once per final tuple to substitute the
///     placeholders against the existing scope-aware resolution rules.</item>
///     <item>Within a single source value, deduplicates emissions that produce the same final
///     tuple by retaining the most-specific target.</item>
/// </list>
/// Across source values for the same entry, an emission whose source had a more specific scope
/// tuple wins over a fan-out emission whose source had a less-specific (or empty) one — this is
/// the rule that makes literal scoped values on the entry override fan-out from a default-scoped
/// sibling that references a variable.
///
/// Sensitivity propagates per-entry: if any sensitive variable contributes to any emission the
/// entry's <see cref="ResolvedEntry.IsSensitive"/> flag is set. Per-tuple sensitivity is out of
/// scope and intentionally not modeled.
///
/// Emissions are emitted in canonical order so that the same project state always produces the
/// same snapshot bytes — preserving the diff-hash determinism property the publish-after-preview
/// 409 gate relies on.
/// </remarks>
internal sealed class ResolvedEntryBuilder
{
    private readonly VariableInterpolator _interpolator;

    public ResolvedEntryBuilder(VariableInterpolator interpolator)
    {
        _interpolator = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
    }

    /// <summary>
    /// Builds the fanned-out resolved entry for a single config entry.
    /// </summary>
    /// <param name="plaintextValues">The entry's source scoped values, with sensitive values already decrypted.</param>
    /// <param name="projectVariables">Project-level plaintext variables keyed by name (checked first).</param>
    /// <param name="globalVariables">Global plaintext variables keyed by name (fallback).</param>
    /// <returns>The resolved values, the unresolved placeholder names, and the sensitivity flag.</returns>
    public ResolvedEntryBuildResult Build(
        IReadOnlyList<ScopedValue> plaintextValues,
        IReadOnlyDictionary<string, PlaintextVariable> projectVariables,
        IReadOnlyDictionary<string, PlaintextVariable> globalVariables)
    {
        ArgumentNullException.ThrowIfNull(plaintextValues);
        ArgumentNullException.ThrowIfNull(projectVariables);
        ArgumentNullException.ThrowIfNull(globalVariables);

        var entryEmissions = new Dictionary<string, Emission>(StringComparer.Ordinal);
        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedSensitive = false;

        foreach (var source in plaintextValues)
        {
            var matches = PlaceholderScanner.PlaceholderPattern.Matches(source.Value);
            var placeholderNames = PlaceholderScanner.ExtractNames(matches);
            var referencedVariables = ResolveReferencedVariables(placeholderNames, projectVariables, globalVariables);
            var targets = referencedVariables.Count == 0 ? [EmptyTuple] : TargetTupleBuilder.Build(referencedVariables);

            var sourceEmissions = new Dictionary<string, Emission>(StringComparer.Ordinal);

            foreach (var target in targets)
            {
                if (!TryMerge(source.Scopes, target, out var final))
                {
                    continue;
                }

                var interpolation = _interpolator.Interpolate(source.Value, matches, final, projectVariables, globalVariables);
                if (interpolation.UsedSensitiveVariable)
                {
                    usedSensitive = true;
                }

                foreach (var name in interpolation.UnresolvedPlaceholders)
                {
                    unresolved.Add(name);
                }

                var key = CanonicalKey(final);
                var emission = new Emission(final, interpolation.Value, source.Scopes.Count, target.Count);

                if (!sourceEmissions.TryGetValue(key, out var existing) || emission.TargetSpecificity > existing.TargetSpecificity)
                {
                    sourceEmissions[key] = emission;
                }
            }

            foreach (var pair in sourceEmissions)
            {
                if (entryEmissions.TryGetValue(pair.Key, out var existing))
                {
                    if (Wins(pair.Value, existing))
                    {
                        entryEmissions[pair.Key] = pair.Value;
                    }
                }
                else
                {
                    entryEmissions[pair.Key] = pair.Value;
                }
            }
        }

        var values = entryEmissions.Values
            .OrderBy(e => CanonicalKey(e.Final), StringComparer.Ordinal)
            .Select(emission => new ScopedValue(emission.Value, ToCanonicalScopeDictionary(emission.Final)))
            .ToList();

        return new ResolvedEntryBuildResult
        {
            Values = values,
            UnresolvedPlaceholders = unresolved,
            UsedSensitiveVariable = usedSensitive,
        };
    }

    private static List<PlaintextVariable> ResolveReferencedVariables(
        IReadOnlyList<string> placeholderNames,
        IReadOnlyDictionary<string, PlaintextVariable> projectVariables,
        IReadOnlyDictionary<string, PlaintextVariable> globalVariables)
    {
        if (placeholderNames.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PlaintextVariable>(placeholderNames.Count);

        foreach (var name in placeholderNames)
        {
            if (!seen.Add(name))
            {
                continue;
            }

            if (projectVariables.TryGetValue(name, out var variable) || globalVariables.TryGetValue(name, out variable))
            {
                result.Add(variable);
            }
        }

        return result;
    }

    private static bool TryMerge(
        Dictionary<string, string> sourceScope,
        IReadOnlyDictionary<string, string> target,
        out Dictionary<string, string> final)
    {
        final = new Dictionary<string, string>(sourceScope.Count + target.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sourceScope)
        {
            final[pair.Key] = pair.Value;
        }

        foreach (var pair in target)
        {
            if (final.TryGetValue(pair.Key, out var existing))
            {
                if (!string.Equals(existing, pair.Value, StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            final[pair.Key] = pair.Value;
        }

        return true;
    }

    /// <summary>
    /// Compares two emissions for the same final scope tuple and returns true when
    /// <paramref name="candidate"/> should replace <paramref name="incumbent"/>.
    /// Order: source-scope specificity (more dims wins, encoding the explicit-wins rule), then
    /// target-scope specificity as a tiebreaker. When both tie, the incumbent stays — and since
    /// the outer loop iterates <c>plaintextValues</c> in store order, that ordering decides the
    /// winner. <see cref="ConfigEntry.Values"/> is materialized from a stable persistence order,
    /// so the choice is deterministic across resolves.
    /// </summary>
    private static bool Wins(Emission candidate, Emission incumbent)
    {
        if (candidate.SourceSpecificity > incumbent.SourceSpecificity)
        {
            return true;
        }

        return candidate.SourceSpecificity == incumbent.SourceSpecificity && candidate.TargetSpecificity > incumbent.TargetSpecificity;
    }

    private static string CanonicalKey(IReadOnlyDictionary<string, string> tuple)
    {
        if (tuple.Count == 0)
        {
            return string.Empty;
        }

        var pairs = tuple.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}");
        return string.Join("|", pairs);
    }

    /// <summary>
    /// Builds a scope dictionary with keys inserted in canonical (ordinal-sorted) order. The diff
    /// hash computation in <see cref="SnapshotResolver"/> already canonicalizes scope keys when
    /// hashing, so this sort is defensive for downstream consumers (Tower preview, audit logs,
    /// ad-hoc snapshot comparisons) that read <see cref="ScopedValue.Scopes"/> directly and would
    /// benefit from a stable iteration order across resolves.
    /// </summary>
    private static Dictionary<string, string> ToCanonicalScopeDictionary(IReadOnlyDictionary<string, string> tuple)
    {
        var result = new Dictionary<string, string>(tuple.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in tuple.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static readonly FrozenDictionary<string, string> EmptyTuple = FrozenDictionary<string, string>.Empty;

    private sealed record Emission(IReadOnlyDictionary<string, string> Final, string Value, int SourceSpecificity, int TargetSpecificity);
}

/// <summary>
/// Output of <see cref="ResolvedEntryBuilder.Build"/> for a single config entry.
/// </summary>
internal sealed record ResolvedEntryBuildResult
{
    /// <summary>
    /// Gets the resolved scope-tuple/value pairs the snapshot will carry for this entry.
    /// </summary>
    public required IList<ScopedValue> Values { get; init; }

    /// <summary>
    /// Gets the placeholder names that could not be resolved against any of the entry's required
    /// target tuples. A non-empty set blocks publish under the strict-unresolved policy.
    /// </summary>
    public required IReadOnlySet<string> UnresolvedPlaceholders { get; init; }

    /// <summary>
    /// Gets a value indicating whether any sensitive variable contributed to any emission. The
    /// caller propagates this onto the entry's <see cref="ResolvedEntry.IsSensitive"/> flag.
    /// </summary>
    public required bool UsedSensitiveVariable { get; init; }
}