using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots.Contracts;

internal static class PaginationQueryExtensions
{
    public static SnapshotListQuery ToStoreQuery(this PaginationQuery query, Guid projectId) => new()
    {
        ProjectId = projectId,
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? "publishedAt",
        SortOrder = query.SortOrder ?? "desc",
    };
}