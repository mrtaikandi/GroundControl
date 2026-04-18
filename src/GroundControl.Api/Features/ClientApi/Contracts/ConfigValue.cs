namespace GroundControl.Api.Features.ClientApi.Contracts;

/// <summary>
/// Represents a single resolved configuration value returned to an authenticated client, including a flag that marks it as sensitive.
/// </summary>
/// <remarks>
/// <see cref="IsSensitive" /> is omitted from the serialised payload when <c>false</c> so that the common, non-sensitive
/// majority of entries carries only <see cref="Value" /> on the wire.
/// </remarks>
internal readonly record struct ConfigValue
{
    /// <summary>
    /// Gets the resolved configuration value as delivered to the client.
    /// </summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry is sensitive and should be protected at rest by the client.
    /// </summary>
    [JsonPropertyName("isSensitive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsSensitive { get; init; }
}