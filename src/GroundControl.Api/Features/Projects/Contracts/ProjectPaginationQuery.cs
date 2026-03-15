using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Projects.Contracts;

internal sealed class ProjectPaginationQuery : PaginationQuery
{
    public Guid? GroupId { get; init; }

    public string? Search { get; init; }
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
        Search = query.Search,
    };
}