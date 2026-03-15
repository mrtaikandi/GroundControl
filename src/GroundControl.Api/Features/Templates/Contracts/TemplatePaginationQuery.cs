using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Templates.Contracts;

internal sealed class TemplatePaginationQuery : PaginationQuery
{
    public Guid? GroupId { get; init; }

    public bool? GlobalOnly { get; init; }
}

internal static class TemplatePaginationQueryExtensions
{
    public static TemplateListQuery ToStoreQuery(this TemplatePaginationQuery query) => new()
    {
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? PaginationQuery.DefaultSortField,
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
        GroupId = query.GroupId,
        GlobalOnly = query.GlobalOnly,
    };
}