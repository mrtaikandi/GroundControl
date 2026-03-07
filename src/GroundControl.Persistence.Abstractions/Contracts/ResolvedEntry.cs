namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a fully resolved configuration entry stored in a snapshot.
/// </summary>
public record ResolvedEntry
{
    /// <summary>
    /// Gets or sets the configuration key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets the serialized value type name.
    /// </summary>
    public required string ValueType { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets or sets the resolved values for each applicable scope combination.
    /// </summary>
    public IList<ScopedValue> Values { get; init; } = [];
}