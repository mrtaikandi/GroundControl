using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.ConfigEntries.Contracts;

internal sealed class ConfigEntryPaginationQuery : PaginationQuery
{
    public Guid? OwnerId { get; init; }

    public ConfigEntryOwnerType? OwnerType { get; init; }

    public string? KeyPrefix { get; init; }
}

internal static class ConfigEntryPaginationQueryExtensions
{
    public static ConfigEntryListQuery ToStoreQuery(this ConfigEntryPaginationQuery query) => new()
    {
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? "key",
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
        OwnerId = query.OwnerId,
        OwnerType = query.OwnerType,
        KeyPrefix = query.KeyPrefix,
    };
}