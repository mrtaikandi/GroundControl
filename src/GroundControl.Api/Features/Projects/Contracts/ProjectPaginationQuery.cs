using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

namespace GroundControl.Api.Features.Projects.Contracts;

internal sealed class ProjectPaginationQuery : PaginationQuery, IValidatableObject
{
    public Guid? GroupId { get; init; }

    public bool? Ungrouped { get; init; }

    public string? Search { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ungrouped == true && GroupId.HasValue)
        {
            yield return new ValidationResult(
                "Ungrouped cannot be combined with GroupId.",
                [nameof(Ungrouped), nameof(GroupId)]);
        }
    }
}

internal static class ProjectPaginationQueryExtensions
{
    public static ProjectListQuery ToStoreQuery(this ProjectPaginationQuery query) => new()
    {
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? PaginationQuery.DefaultSortField,
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
        GroupId = query.GroupId,
        Ungrouped = query.Ungrouped ?? false,
        Search = query.Search,
    };
}