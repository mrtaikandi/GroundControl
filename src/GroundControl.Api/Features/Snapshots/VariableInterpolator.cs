using System.Text.RegularExpressions;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots;

/// <summary>
/// Resolves <c>{{variableName}}</c> placeholders in config entry values using a two-tier
/// variable system (project-level first, then global) with scope-aware resolution.
/// </summary>
internal sealed partial class VariableInterpolator
{
    private readonly IScopeResolver _scopeResolver;

    public VariableInterpolator(IScopeResolver scopeResolver)
    {
        _scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
    }

    /// <summary>
    /// Interpolates <c>{{variableName}}</c> placeholders in the given value string.
    /// </summary>
    /// <param name="value">The raw config entry value potentially containing placeholders.</param>
    /// <param name="clientScopes">The client's scope dimension-value pairs for resolution.</param>
    /// <param name="projectVariables">Project-level plaintext variables keyed by variable name (checked first).</param>
    /// <param name="globalVariables">Global plaintext variables keyed by variable name (fallback).</param>
    /// <returns>
    /// The interpolation result containing the resolved value, any unresolved placeholder names,
    /// and whether a sensitive variable contributed to the resolved value.
    /// </returns>
    public InterpolationResult Interpolate(
        string value,
        IReadOnlyDictionary<string, string> clientScopes,
        IReadOnlyDictionary<string, PlaintextVariable> projectVariables,
        IReadOnlyDictionary<string, PlaintextVariable> globalVariables)
    {
        var matches = PlaceholderPattern.Matches(value);
        if (matches.Count == 0)
        {
            return new InterpolationResult { Value = value, UnresolvedPlaceholders = [], UsedSensitiveVariable = false };
        }

        var unresolved = new List<string>();
        var usedSensitive = false;
        var result = PlaceholderPattern.Replace(value, match =>
        {
            var name = match.Groups[1].Value;

            // Two-tier lookup: project variables take priority over global
            var resolved = TryResolve(name, projectVariables, clientScopes) ?? TryResolve(name, globalVariables, clientScopes);
            if (resolved is not null)
            {
                if (resolved.IsSensitive)
                {
                    usedSensitive = true;
                }

                return resolved.Value;
            }

            unresolved.Add(name);
            return match.Value;
        });

        return new InterpolationResult { Value = result, UnresolvedPlaceholders = unresolved, UsedSensitiveVariable = usedSensitive };
    }

    private ResolvedVariable? TryResolve(string variableName, IReadOnlyDictionary<string, PlaintextVariable> variables, IReadOnlyDictionary<string, string> clientScopes)
    {
        if (!variables.TryGetValue(variableName, out var variable))
        {
            return null;
        }

        var scopedValue = _scopeResolver.Resolve(variable.Values, clientScopes);
        return scopedValue is null ? null : new ResolvedVariable(scopedValue.Value, variable.IsSensitive);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern { get; }

    private sealed record ResolvedVariable(string Value, bool IsSensitive);
}

/// <summary>
/// Variable projection used by <see cref="VariableInterpolator"/>. Holds only the fields the
/// interpolator reads (decrypted scoped values plus the sensitivity flag), so the publisher can
/// build this lookup without mutating freshly-loaded <see cref="Variable"/> entities.
/// </summary>
public sealed record PlaintextVariable
{
    /// <summary>
    /// Gets the plaintext scoped values for this variable.
    /// </summary>
    public required IReadOnlyList<ScopedValue> Values { get; init; }

    /// <summary>
    /// Gets a value indicating whether the variable is sensitive. Resolved placeholders that
    /// reference a sensitive variable propagate sensitivity to the resulting snapshot entry.
    /// </summary>
    public required bool IsSensitive { get; init; }
}

/// <summary>
/// Represents the result of variable interpolation on a config entry value.
/// </summary>
public sealed record InterpolationResult
{
    /// <summary>
    /// Gets the interpolated value string with placeholders replaced where possible.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the list of placeholder names that could not be resolved.
    /// </summary>
    public required IReadOnlyList<string> UnresolvedPlaceholders { get; init; }

    /// <summary>
    /// Gets a value indicating whether a sensitive variable contributed to the resolved value.
    /// </summary>
    public bool UsedSensitiveVariable { get; init; }

    /// <summary>
    /// Gets a value indicating whether all placeholders were successfully resolved.
    /// </summary>
    public bool IsFullyResolved => UnresolvedPlaceholders.Count == 0;
}