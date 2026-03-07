using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for template entities.
/// </summary>
public interface ITemplateStore
{
    /// <summary>
    /// Gets a template by its unique identifier.
    /// </summary>
    Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists templates with paging, sorting, and optional filters.
    /// </summary>
    Task<PagedResult<Template>> ListAsync(TemplateListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template.
    /// </summary>
    Task CreateAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Template template, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the template is referenced by any projects.
    /// </summary>
    Task<bool> IsReferencedByProjectsAsync(Guid templateId, CancellationToken cancellationToken = default);
}