using System.Text.RegularExpressions;

namespace GroundControl.Api.Features.Snapshots;

/// <summary>
/// Scans a string for <c>{{name}}</c> placeholders and extracts the names.
/// </summary>
internal static partial class PlaceholderScanner
{
    /// <summary>
    /// Gets the regex used to identify <c>{{name}}</c> placeholders. The single capture group is
    /// the placeholder name. Exposed to <see cref="VariableInterpolator"/> so the substitution
    /// path uses the exact same pattern as the scan path.
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    internal static partial Regex PlaceholderPattern { get; }

    /// <summary>
    /// Extracts the placeholder names from a pre-computed match collection.
    /// </summary>
    /// <remarks>
    /// Split from the regex run so a caller (notably <see cref="ResolvedEntryBuilder"/>) that fans the
    /// same source value out across many target tuples can pass the same <see cref="MatchCollection"/>
    /// to both the scan-for-names step and the per-tuple substitute step in <see cref="VariableInterpolator"/>,
    /// avoiding M extra regex passes per source value.
    /// </remarks>
    public static IReadOnlyList<string> ExtractNames(MatchCollection matches)
    {
        if (matches.Count == 0)
        {
            return [];
        }

        var names = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            names.Add(match.Groups[1].Value);
        }

        return names;
    }
}