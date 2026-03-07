using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for scope entities.
/// </summary>
public interface IScopeStore
{
    /// <summary>
    /// Gets a scope by its unique identifier.
    /// </summary>
    Task<Scope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a scope by its dimension name.
    /// </summary>
    Task<Scope?> GetByDimensionAsync(string dimension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists scopes with paging and sorting.
    /// </summary>
    Task<PagedResult<Scope>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new scope.
    /// </summary>
    Task CreateAsync(Scope scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing scope with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Scope scope, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a scope with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a scope dimension value is referenced by any entity.
    /// </summary>
    Task<bool> IsReferencedAsync(string dimension, string value, CancellationToken cancellationToken = default);
}