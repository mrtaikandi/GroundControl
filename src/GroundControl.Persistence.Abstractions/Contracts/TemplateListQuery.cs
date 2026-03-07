namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing templates.
/// </summary>
public class TemplateListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the owning group identifier filter.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only global templates should be returned.
    /// </summary>
    public bool? GlobalOnly { get; set; }
}