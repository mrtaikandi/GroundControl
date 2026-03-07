namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a base query for paged list operations.
/// </summary>
public class ListQuery
{
    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    public int Limit { get; set; } = 25;

    /// <summary>
    /// Gets or sets the pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets the field used for sorting.
    /// </summary>
    public string SortField { get; set; } = "name";

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public string SortOrder { get; set; } = "asc";
}