namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a field-level change captured in an audit record.
/// </summary>
public record FieldChange
{
    /// <summary>
    /// Gets or sets the name of the changed field.
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// Gets or sets the previous serialized field value.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// Gets or sets the new serialized field value.
    /// </summary>
    public string? NewValue { get; init; }
}