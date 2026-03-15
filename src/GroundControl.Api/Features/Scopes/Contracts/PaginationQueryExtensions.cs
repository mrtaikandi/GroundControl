using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Scopes.Contracts;

internal static class PaginationQueryExtensions
{
    public static ListQuery ToStoreQuery(this PaginationQuery query)
    {
        var sortField = query.SortField ?? PaginationQuery.DefaultSortField;
        if (string.Equals(sortField, "name", StringComparison.OrdinalIgnoreCase))
        {
            sortField = "dimension";
        }

        return new ListQuery
        {
            Limit = query.Limit ?? PaginationQuery.DefaultLimit,
            After = query.After,
            Before = query.Before,
            SortField = sortField,
            SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
        };
    }
}