namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a value variant for a specific scope combination.
/// </summary>
public record ScopedValue
{
    /// <summary>
    /// Gets or sets the scope dimension-value pairs that qualify this value.
    /// </summary>
    public Dictionary<string, string> Scopes { get; init; } = [];

    /// <summary>
    /// Gets or sets the serialized value for the scope combination.
    /// </summary>
    public required string Value { get; init; }
}