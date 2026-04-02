using System.Text.Json;
using System.Text.Json.Serialization;

namespace GroundControl.Cli.Shared.Parsing;

internal static partial class ScopedValueParser
{
    internal sealed record ParsedScopedValue(IReadOnlyDictionary<string, string> Scopes, string Value);

    internal static List<ParsedScopedValue> Parse(IReadOnlyList<string>? values, string? valuesJson)
    {
        if (valuesJson is not null)
        {
            return ParseJson(valuesJson);
        }

        if (values is null or { Count: 0 })
        {
            return [];
        }

        var result = new List<ParsedScopedValue>(values.Count);
        foreach (var input in values)
        {
            result.Add(ParseSingle(input));
        }

        return result;
    }

    internal static ParsedScopedValue ParseSingle(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var equalsIndex = input.IndexOf('=', StringComparison.Ordinal);
        if (equalsIndex < 0)
        {
            throw new FormatException(
                $"Invalid scoped value format: '{input}'. Expected 'default=value' or 'dimension:scopeValue=value'.");
        }

        var scopePart = input[..equalsIndex];
        var valuePart = input[(equalsIndex + 1)..];

        if (string.Equals(scopePart, "default", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedScopedValue(new Dictionary<string, string>(), valuePart);
        }

        var scopes = ParseScopes(scopePart, input);
        return new ParsedScopedValue(scopes, valuePart);
    }

    private static Dictionary<string, string> ParseScopes(string scopePart, string originalInput)
    {
        var scopes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var segments = scopePart.Split(',');

        foreach (var segment in segments)
        {
            var colonIndex = segment.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex < 0)
            {
                throw new FormatException(
                    $"Invalid scope qualifier: '{segment}' in '{originalInput}'. Expected 'dimension:scopeValue'.");
            }

            var dimension = segment[..colonIndex].Trim();
            var scopeValue = segment[(colonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(dimension) || string.IsNullOrEmpty(scopeValue))
            {
                throw new FormatException(
                    $"Invalid scope qualifier: '{segment}' in '{originalInput}'. Both dimension and value are required.");
            }

            if (!scopes.TryAdd(dimension, scopeValue))
            {
                throw new FormatException(
                    $"Duplicate scope dimension: '{dimension}' in '{originalInput}'.");
            }
        }

        return scopes;
    }

    private static List<ParsedScopedValue> ParseJson(string json)
    {
        try
        {
            var items = JsonSerializer.Deserialize(json, ScopedValueJsonContext.Default.ListScopedValueJsonModel);
            if (items is null)
            {
                throw new FormatException("The JSON input deserialized to null.");
            }

            var result = new List<ParsedScopedValue>(items.Count);
            foreach (var item in items)
            {
                var scopes = item.Scopes ?? new Dictionary<string, string>();
                var value = item.Value ?? throw new FormatException("Each scoped value must have a non-null 'value' property.");
                result.Add(new ParsedScopedValue(scopes, value));
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid JSON input for scoped values: {ex.Message}", ex);
        }
    }

    internal sealed class ScopedValueJsonModel
    {
        [JsonPropertyName("scopes")]
        public Dictionary<string, string>? Scopes { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    [JsonSerializable(typeof(List<ScopedValueJsonModel>))]
    internal sealed partial class ScopedValueJsonContext : JsonSerializerContext;
}