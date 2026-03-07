namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents an immutable audit log entry.
/// </summary>
public class AuditRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for the audit record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the changed entity type.
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the changed entity identifier.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Gets or sets the owning group identifier for scoped queries.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the action performed.
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier.
    /// </summary>
    public Guid PerformedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action occurred.
    /// </summary>
    public DateTimeOffset PerformedAt { get; set; }

    /// <summary>
    /// Gets or sets the field-level changes captured for the action.
    /// </summary>
    public IList<FieldChange> Changes { get; init; } = [];

    /// <summary>
    /// Gets or sets the additional metadata captured for the action.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}