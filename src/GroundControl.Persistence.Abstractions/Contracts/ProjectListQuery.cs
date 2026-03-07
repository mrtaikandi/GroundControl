namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing projects.
/// </summary>
public class ProjectListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the owning group identifier filter.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the optional text search filter.
    /// </summary>
    public string? Search { get; set; }
}