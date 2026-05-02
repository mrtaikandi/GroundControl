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
    /// <param name="projectVariables">Project-level variables keyed by variable name (checked first).</param>
    /// <param name="globalVariables">Global variables keyed by variable name (fallback).</param>
    /// <returns>
    /// The interpolation result containing the resolved value, any unresolved placeholder names,
    /// and whether a sensitive variable contributed to the resolved value.
    /// </returns>
    public InterpolationResult Interpolate(
        string value,
        IReadOnlyDictionary<string, string> clientScopes,
        IReadOnlyDictionary<string, Variable> projectVariables,
        IReadOnlyDictionary<string, Variable> globalVariables)
    {
        var matches = PlaceholderPattern.Matches(value);
        if (matches.Count == 0)
        {
            return new InterpolationResult(value, [], UsedSensitiveVariable: false);
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

        return new InterpolationResult(result, unresolved, usedSensitive);
    }

    private ResolvedVariable? TryResolve(string variableName, IReadOnlyDictionary<string, Variable> variables, IReadOnlyDictionary<string, string> clientScopes)
    {
        if (!variables.TryGetValue(variableName, out var variable))
        {
            return null;
        }

        var values = variable.Values as IReadOnlyList<ScopedValue> ?? [.. variable.Values];
        var scopedValue = _scopeResolver.Resolve(values, clientScopes);

        return scopedValue is null ? null : new ResolvedVariable(scopedValue.Value, variable.IsSensitive);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern { get; }

    private sealed record ResolvedVariable(string Value, bool IsSensitive);
}

/// <summary>
/// Represents the result of variable interpolation on a config entry value.
/// </summary>
/// <param name="Value">The interpolated value string with placeholders replaced where possible.</param>
/// <param name="UnresolvedPlaceholders">A list of placeholder names that could not be resolved.</param>
/// <param name="UsedSensitiveVariable">A value indicating whether a sensitive variable contributed
/// to the resolved value.</param>
public record InterpolationResult(string Value, IReadOnlyList<string> UnresolvedPlaceholders, bool UsedSensitiveVariable = false)
{
    /// <summary>
    /// Gets a value indicating whether all placeholders were successfully resolved.
    /// </summary>
    public bool IsFullyResolved => UnresolvedPlaceholders.Count == 0;
}