using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Clients.Contracts;

internal sealed class ClientPaginationQuery : PaginationQuery;

internal static class ClientPaginationQueryExtensions
{
    public static ClientListQuery ToStoreQuery(this ClientPaginationQuery query, Guid projectId) => new()
    {
        ProjectId = projectId,
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? PaginationQuery.DefaultSortField,
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
    };
}