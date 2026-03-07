namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing variables.
/// </summary>
public class VariableListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the variable scope tier filter.
    /// </summary>
    public VariableScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the owning group identifier filter.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the owning project identifier filter.
    /// </summary>
    public Guid? ProjectId { get; set; }
}