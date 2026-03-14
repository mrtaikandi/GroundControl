namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing snapshots.
/// </summary>
public class SnapshotListQuery : ListQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotListQuery"/> class
    /// with defaults suited for snapshot listing (sorted by publishedAt descending).
    /// </summary>
    public SnapshotListQuery()
    {
        SortField = "publishedAt";
        SortOrder = "desc";
    }

    /// <summary>
    /// Gets or sets the project identifier whose snapshots should be listed.
    /// </summary>
    public required Guid ProjectId { get; set; }
}