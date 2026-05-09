using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Projects;

internal sealed class ListGroupedProjectsHandler : IEndpointHandler
{
    private const int MaxGroups = 10;

    private readonly IGroupStore _groupStore;
    private readonly IProjectStore _projectStore;
    private readonly ILogger<ListGroupedProjectsHandler> _logger;

    public ListGroupedProjectsHandler(IGroupStore groupStore, IProjectStore projectStore, ILogger<ListGroupedProjectsHandler> logger)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _projectStore = projectStore ?? throw new ArgumentNullException(nameof(projectStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/grouped", async (
                [AsParameters] GroupedProjectsQuery query,
                [FromServices] ListGroupedProjectsHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(query, cancellationToken))
            .RequireAuthorization(Permissions.ProjectsRead)
            .WithSummary("List projects grouped by owning group")
            .WithDescription(
                "Returns the first page of projects for every group plus an ungrouped bucket, all sorted by name ascending. "
                + "Sections whose project list is empty after applying the search filter are omitted. "
                + "Use the returned per-section cursor with GET /api/projects to fetch the next page.")
            .Produces<GroupedProjectsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName(nameof(ListGroupedProjectsHandler));
    }

    private async Task<IResult> HandleAsync(GroupedProjectsQuery query, CancellationToken cancellationToken = default)
    {
        var perGroup = query.PerGroup ?? GroupedProjectsQuery.DefaultPerGroup;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search;

        var groupsPage = await _groupStore.ListAsync(
            new ListQuery
            {
                Limit = MaxGroups,
                SortField = "name",
                SortOrder = "asc"
            },
            cancellationToken);

        if (groupsPage.NextCursor is not null)
        {
            _logger.LogGroupsSoftCapHit(MaxGroups, groupsPage.TotalCount);
        }

        var groupTasks = groupsPage.Items
            .Select(group => LoadGroupSectionAsync(group, search, perGroup, cancellationToken))
            .ToList();

        var ungroupedTask = LoadUngroupedSectionAsync(search, perGroup, cancellationToken);

        var groupResults = await Task.WhenAll(groupTasks);
        var ungrouped = await ungroupedTask;

        var sections = groupResults
            .Where(section => section is not null)
            .Select(section => section!)
            .ToList();

        return TypedResults.Ok(new GroupedProjectsResponse
        {
            Groups = sections,
            Ungrouped = ungrouped
        });
    }

    private async Task<GroupProjects?> LoadGroupSectionAsync(Group group, string? search, int perGroup, CancellationToken cancellationToken)
    {
        var page = await _projectStore.ListAsync(
            new ProjectListQuery
            {
                Limit = perGroup,
                SortField = "name",
                SortOrder = "asc",
                GroupId = group.Id,
                Search = search
            },
            cancellationToken);

        if (page.TotalCount == 0)
        {
            return null;
        }

        return new GroupProjects
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            TotalCount = page.TotalCount,
            Projects = page.Items.Select(ProjectResponse.From).ToList(),
            NextCursor = page.NextCursor
        };
    }

    private async Task<UngroupedProjects?> LoadUngroupedSectionAsync(string? search, int perGroup, CancellationToken cancellationToken)
    {
        var page = await _projectStore.ListAsync(
            new ProjectListQuery
            {
                Limit = perGroup,
                SortField = "name",
                SortOrder = "asc",
                Ungrouped = true,
                Search = search
            },
            cancellationToken);

        if (page.TotalCount == 0)
        {
            return null;
        }

        return new UngroupedProjects
        {
            TotalCount = page.TotalCount,
            Projects = page.Items.Select(ProjectResponse.From).ToList(),
            NextCursor = page.NextCursor
        };
    }
}

internal static partial class ListGroupedProjectsHandlerLogs
{
    [LoggerMessage(1, LogLevel.Warning, "Grouped projects endpoint hit the soft cap of {Cap} groups (total groups: {TotalCount}); sections beyond the cap are omitted from the response.")]
    public static partial void LogGroupsSoftCapHit(this ILogger<ListGroupedProjectsHandler> logger, int cap, long totalCount);
}