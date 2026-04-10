using System.Globalization;
using System.Text.Json;

namespace GroundControl.Link.Internals;


/// <summary>
/// Parses configuration JSON payloads from the GroundControl server into
/// flattened key-value dictionaries compatible with the .NET configuration system.
/// </summary>
internal static class ConfigurationParser
{
    private const string DataPropertyName = "data";
    private const string SnapshotVersionPropertyName = "snapshotVersion";

    /// <summary>
    /// Parses a configuration JSON payload, flattening nested <c>data</c> properties
    /// into colon-separated keys and extracting the optional <c>snapshotVersion</c>.
    /// </summary>
    public static ParsedConfiguration Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty(DataPropertyName, out var data))
        {
            FlattenElement(data, string.Empty, config);
        }

        string? snapshotVersion = null;
        if (doc.RootElement.TryGetProperty(SnapshotVersionPropertyName, out var version))
        {
            snapshotVersion = version.GetInt64().ToString(CultureInfo.InvariantCulture);
        }

        return new ParsedConfiguration { Config = config, SnapshotVersion = snapshotVersion };
    }

    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prefix.Length > 0 ? $"{prefix}:{prop.Name}" : prop.Name;
                    FlattenElement(prop.Value, key, result);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{prefix}:{index++}", result);
                }

                break;

            case JsonValueKind.Null:
                break;

            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            default:
                result[prefix] = element.ToString();
                break;
        }
    }

    /// <summary>
    /// Represents the result of parsing a configuration JSON payload.
    /// </summary>
    internal readonly record struct ParsedConfiguration
    {
        /// <summary>
        /// Gets the flattened configuration entries with colon-separated keys.
        /// </summary>
        public required Dictionary<string, string> Config { get; init; }

        /// <summary>
        /// Gets the snapshot version, or <see langword="null" /> if not present.
        /// </summary>
        public string? SnapshotVersion { get; init; }
    }
}