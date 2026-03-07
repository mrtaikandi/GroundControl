namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing snapshots.
/// </summary>
public class SnapshotListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the project identifier whose snapshots should be listed.
    /// </summary>
    public required Guid ProjectId { get; set; }
}