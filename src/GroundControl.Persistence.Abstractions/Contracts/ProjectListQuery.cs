using System.ComponentModel.DataAnnotations;

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
    /// Gets or sets a value indicating whether to return only projects that have no owning group.
    /// </summary>
    public bool Ungrouped { get; set; }

    /// <summary>
    /// Gets or sets the optional text search filter.
    /// </summary>
    public string? Search { get; set; }

    /// <inheritdoc />
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext))
        {
            yield return result;
        }

        if (Ungrouped && GroupId.HasValue)
        {
            yield return new ValidationResult("Ungrouped cannot be combined with GroupId.", [nameof(Ungrouped), nameof(GroupId)]);
        }
    }
}