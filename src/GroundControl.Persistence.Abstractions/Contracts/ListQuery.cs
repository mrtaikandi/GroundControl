using System.ComponentModel.DataAnnotations;

namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a base query for bidirectional paged list operations.
/// </summary>
public class ListQuery : IValidatableObject
{
    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    [Range(1, 100)]
    public int Limit { get; set; } = 25;

    /// <summary>
    /// Gets or sets the cursor for the next page after the current boundary item.
    /// </summary>
    public string? After { get; set; }

    /// <summary>
    /// Gets or sets the cursor for the previous page before the current boundary item.
    /// </summary>
    public string? Before { get; set; }

    /// <summary>
    /// Gets or sets the field used for sorting.
    /// </summary>
    public string SortField { get; set; } = "name";

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public string SortOrder { get; set; } = "asc";

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(After) && !string.IsNullOrWhiteSpace(Before))
        {
            yield return new ValidationResult("After and Before cannot both be specified.", [nameof(After), nameof(Before)]);
        }
    }
}