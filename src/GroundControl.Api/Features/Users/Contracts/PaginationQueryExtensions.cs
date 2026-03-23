using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Users.Contracts;

internal static class PaginationQueryExtensions
{
    public static ListQuery ToStoreQuery(this PaginationQuery query) => new()
    {
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? "username",
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
    };
}