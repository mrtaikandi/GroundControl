namespace GroundControl.Api.Features.Projects.Contracts;

/// <summary>
/// Represents a project listing partitioned by owning group, with a separate bucket for ungrouped projects.
/// </summary>
internal sealed record GroupedProjectsResponse
{
    /// <summary>
    /// Gets the per-group sections, sorted by group name ascending. Sections whose project list is empty
    /// after applying the search filter are omitted.
    /// </summary>
    public required IReadOnlyList<GroupProjects> Groups { get; init; }

    /// <summary>
    /// Gets the bucket of projects that have no owning group, or <see langword="null" /> when no ungrouped
    /// projects match the current filter.
    /// </summary>
    public UngroupedProjects? Ungrouped { get; init; }
}