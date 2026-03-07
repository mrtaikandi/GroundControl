using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for project entities.
/// </summary>
public interface IProjectStore
{
    /// <summary>
    /// Gets a project by its unique identifier.
    /// </summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists projects with paging, sorting, and optional filters.
    /// </summary>
    Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new project.
    /// </summary>
    Task CreateAsync(Project project, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing project with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a project with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a snapshot for a project with optimistic concurrency.
    /// </summary>
    Task<bool> ActivateSnapshotAsync(Guid projectId, Guid snapshotId, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the IDs of all projects that reference a given template.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetProjectIdsReferencingTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
}