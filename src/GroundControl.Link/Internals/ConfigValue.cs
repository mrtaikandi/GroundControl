using System.Text.Json.Serialization;

namespace GroundControl.Link.Internals;

/// <summary>
/// A single configuration entry value carried through the Link's internal pipeline, along with its sensitivity flag.
/// </summary>
/// <remarks>
/// Mirrors the wire contract emitted by the server. <see cref="IsSensitive" /> defaults to <c>false</c> and is omitted
/// from JSON when unset, so non-sensitive entries stay compact on the wire and only the minority of sensitive entries carry the flag.
/// </remarks>
internal readonly record struct ConfigValue
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("isSensitive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsSensitive { get; init; }
}