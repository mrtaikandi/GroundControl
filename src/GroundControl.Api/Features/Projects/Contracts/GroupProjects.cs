namespace GroundControl.Api.Features.Projects.Contracts;

/// <summary>
/// Represents a single group section within a grouped project listing.
/// </summary>
internal sealed record GroupProjects
{
    /// <summary>
    /// Gets the group identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the group display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional group description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the total number of projects in this group that match the current filter.
    /// </summary>
    public required long TotalCount { get; init; }

    /// <summary>
    /// Gets the first page of matching projects in this group, sorted by name ascending.
    /// </summary>
    public required IReadOnlyList<ProjectResponse> Projects { get; init; }

    /// <summary>
    /// Gets the cursor used to fetch the next page via <c>GET /api/projects?groupId=&amp;after=</c>, or
    /// <see langword="null" /> when no more pages exist.
    /// </summary>
    public string? NextCursor { get; init; }
}