namespace GroundControl.Api.Features.Projects.Contracts;

/// <summary>
/// Represents the bucket of projects that have no owning group.
/// </summary>
internal sealed record UngroupedProjects
{
    /// <summary>
    /// Gets the total number of ungrouped projects that match the current filter.
    /// </summary>
    public required long TotalCount { get; init; }

    /// <summary>
    /// Gets the first page of matching ungrouped projects, sorted by name ascending.
    /// </summary>
    public required IReadOnlyList<ProjectResponse> Projects { get; init; }

    /// <summary>
    /// Gets the cursor used to fetch the next page via <c>GET /api/projects?ungrouped=true&amp;after=</c>,
    /// or <see langword="null" /> when no more pages exist.
    /// </summary>
    public string? NextCursor { get; init; }
}