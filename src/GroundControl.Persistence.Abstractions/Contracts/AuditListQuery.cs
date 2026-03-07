namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing audit records.
/// </summary>
public class AuditListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the entity type filter.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier filter.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier filter.
    /// </summary>
    public Guid? PerformedBy { get; set; }

    /// <summary>
    /// Gets or sets the inclusive start of the time range filter.
    /// </summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets or sets the inclusive end of the time range filter.
    /// </summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets or sets the accessible group identifiers used for scoped filtering.
    /// </summary>
    public IReadOnlyList<Guid?>? AccessibleGroupIds { get; set; }
}