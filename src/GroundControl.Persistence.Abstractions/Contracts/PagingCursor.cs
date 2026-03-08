namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents the logical boundary item for bidirectional cursor pagination.
/// </summary>
public sealed record PagingCursor
{
    /// <summary>
    /// Gets or sets the entity identifier used as a deterministic tie-breaker.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the sort field value captured for the boundary item.
    /// </summary>
    public object? SortValue { get; init; }

    /// <summary>
    /// Gets or sets the logical sort field that produced the cursor.
    /// </summary>
    public required string SortField { get; init; }

    /// <summary>
    /// Gets or sets the logical sort order that produced the cursor.
    /// </summary>
    public required string SortOrder { get; init; }

    /// <summary>
    /// Gets or sets the cursor payload version.
    /// </summary>
    public int Version { get; init; } = 1;
}