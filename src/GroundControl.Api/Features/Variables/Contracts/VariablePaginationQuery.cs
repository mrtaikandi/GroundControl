using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Variables.Contracts;

internal sealed class VariablePaginationQuery : PaginationQuery
{
    public VariableScope? Scope { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? ProjectId { get; init; }
}

internal static class VariablePaginationQueryExtensions
{
    public static VariableListQuery ToStoreQuery(this VariablePaginationQuery query) => new()
    {
        Limit = query.Limit ?? PaginationQuery.DefaultLimit,
        After = query.After,
        Before = query.Before,
        SortField = query.SortField ?? PaginationQuery.DefaultSortField,
        SortOrder = query.SortOrder ?? PaginationQuery.DefaultSortOrder,
        Scope = query.Scope,
        GroupId = query.GroupId,
        ProjectId = query.ProjectId,
    };
}