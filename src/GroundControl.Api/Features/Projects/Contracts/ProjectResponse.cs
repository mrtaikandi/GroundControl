using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Projects.Contracts;

/// <summary>
/// Represents the API response body for a project.
/// </summary>
internal sealed record ProjectResponse
{
    /// <summary>
    /// Gets the unique identifier for the project.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional project description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the owning group identifier.
    /// </summary>
    public Guid? GroupId { get; init; }

    /// <summary>
    /// Gets the ordered template identifiers applied to the project.
    /// </summary>
    public required IReadOnlyList<Guid> TemplateIds { get; init; }

    /// <summary>
    /// Gets the active snapshot identifier.
    /// </summary>
    public Guid? ActiveSnapshotId { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the project was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the project.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the project was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the project.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Project" /> entity.
    /// </summary>
    /// <param name="project">The persisted project entity.</param>
    /// <returns>The API response contract.</returns>
    public static ProjectResponse From(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            GroupId = project.GroupId,
            TemplateIds = [.. project.TemplateIds],
            ActiveSnapshotId = project.ActiveSnapshotId,
            Version = project.Version,
            CreatedAt = project.CreatedAt,
            CreatedBy = project.CreatedBy,
            UpdatedAt = project.UpdatedAt,
            UpdatedBy = project.UpdatedBy,
        };
    }
}