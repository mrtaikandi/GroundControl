using System.Globalization;
using System.Text.Json;

namespace GroundControl.Link.Internals;


/// <summary>
/// Parses configuration JSON payloads from the GroundControl server into flattened key-value dictionaries compatible with the .NET configuration system.
/// </summary>
internal static class ConfigurationParser
{
    private const string DataPropertyName = "data";
    private const string SnapshotVersionPropertyName = "snapshotVersion";
    private const string ValuePropertyName = "value";
    private const string IsSensitivePropertyName = "isSensitive";

    /// <summary>
    /// Parses a configuration JSON payload, flattening nested <c>data</c> properties into colon-separated keys and extracting the optional <c>snapshotVersion</c>.
    /// </summary>
    public static ParsedConfiguration Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var config = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty(DataPropertyName, out var data) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in data.EnumerateObject())
            {
                ParseEntry(prop.Name, prop.Value, config);
            }
        }

        string? snapshotVersion = null;
        if (doc.RootElement.TryGetProperty(SnapshotVersionPropertyName, out var version))
        {
            snapshotVersion = version.GetInt64().ToString(CultureInfo.InvariantCulture);
        }

        return new ParsedConfiguration { Config = config, SnapshotVersion = snapshotVersion };
    }

    private static void ParseEntry(string key, JsonElement entry, Dictionary<string, ConfigValue> result)
    {
        // Expected shape: {"value": "...", "isSensitive": true?}. Non-sensitive entries omit the flag.
        if (entry.ValueKind != JsonValueKind.Object)
        {
            // Defensive: treat a bare scalar as {Value: <scalar>, IsSensitive: false} so older or malformed payloads don't crash the Link.
            FlattenValue(entry, key, isSensitive: false, result);
            return;
        }

        if (!entry.TryGetProperty(ValuePropertyName, out var value))
        {
            return;
        }

        var isSensitive = entry.TryGetProperty(IsSensitivePropertyName, out var flag) && flag.ValueKind == JsonValueKind.True;
        FlattenValue(value, key, isSensitive, result);
    }

    private static void FlattenValue(JsonElement element, string prefix, bool isSensitive, Dictionary<string, ConfigValue> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prefix.Length > 0 ? $"{prefix}:{prop.Name}" : prop.Name;
                    FlattenValue(prop.Value, key, isSensitive, result);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenValue(item, $"{prefix}:{index++}", isSensitive, result);
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
                result[prefix] = new ConfigValue { Value = element.ToString(), IsSensitive = isSensitive };
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
        public required Dictionary<string, ConfigValue> Config { get; init; }

        /// <summary>
        /// Gets the snapshot version, or <see langword="null" /> if not present.
        /// </summary>
        public string? SnapshotVersion { get; init; }
    }
}