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
    /// <returns>The interpolation result containing the resolved value and any unresolved placeholder names.</returns>
    public InterpolationResult Interpolate(
        string value,
        IReadOnlyDictionary<string, string> clientScopes,
        IReadOnlyDictionary<string, Variable> projectVariables,
        IReadOnlyDictionary<string, Variable> globalVariables)
    {
        var matches = PlaceholderPattern.Matches(value);
        if (matches.Count == 0)
        {
            return new InterpolationResult(value, []);
        }

        var unresolved = new List<string>();
        var result = PlaceholderPattern.Replace(value, match =>
        {
            var name = match.Groups[1].Value;

            // Two-tier lookup: project variables take priority over global
            var resolved = TryResolve(name, projectVariables, clientScopes) ?? TryResolve(name, globalVariables, clientScopes);
            if (resolved is not null)
            {
                return resolved;
            }

            unresolved.Add(name);
            return match.Value;
        });

        return new InterpolationResult(result, unresolved);
    }

    private string? TryResolve(string variableName, IReadOnlyDictionary<string, Variable> variables, IReadOnlyDictionary<string, string> clientScopes)
    {
        if (!variables.TryGetValue(variableName, out var variable))
        {
            return null;
        }

        var values = variable.Values as IReadOnlyList<ScopedValue> ?? [.. variable.Values];
        var scopedValue = _scopeResolver.Resolve(values, clientScopes);

        return scopedValue?.Value;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern { get; }
}

/// <summary>
/// Represents the result of variable interpolation on a config entry value.
/// </summary>
/// <param name="Value">The interpolated value string with placeholders replaced where possible.</param>
/// <param name="UnresolvedPlaceholders">A list of placeholder names that could not be resolved.</param>
public record InterpolationResult(string Value, IReadOnlyList<string> UnresolvedPlaceholders)
{
    /// <summary>
    /// Gets a value indicating whether all placeholders were successfully resolved.
    /// </summary>
    public bool IsFullyResolved => UnresolvedPlaceholders.Count == 0;
}