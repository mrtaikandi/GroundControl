using System.Diagnostics.CodeAnalysis;

namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a value variant for a specific scope combination.
/// </summary>
public record ScopedValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedValue"/> record with the specified value and scopes.
    /// </summary>
    /// <param name="value">The serialized value for the scope combination.</param>
    /// <param name="scopes">The scope dimension-value pairs that qualify this value.</param>
    [SetsRequiredMembers]
    public ScopedValue(string value, Dictionary<string, string> scopes)
        : this()
    {
        Value = value;
        Scopes = scopes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedValue"/> record.
    /// </summary>
    public ScopedValue() { }

    /// <summary>
    /// Gets or sets the scope dimension-value pairs that qualify this value.
    /// </summary>
    public Dictionary<string, string> Scopes { get; init; } = [];

    /// <summary>
    /// Gets or sets the serialized value for the scope combination.
    /// </summary>
    public required string Value { get; init; }
}