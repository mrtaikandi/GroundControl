using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for variable entities.
/// </summary>
public interface IVariableStore
{
    /// <summary>
    /// Gets a variable by its unique identifier.
    /// </summary>
    Task<Variable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists variables with paging, sorting, and optional filters.
    /// </summary>
    Task<PagedResult<Variable>> ListAsync(VariableListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    Task CreateAsync(Variable variable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing variable with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Variable variable, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a variable with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all global-scoped variables for a group.
    /// </summary>
    Task<IReadOnlyList<Variable>> GetGlobalVariablesForGroupAsync(Guid? groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all variables scoped to a specific project.
    /// </summary>
    Task<IReadOnlyList<Variable>> GetProjectVariablesAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the variable is referenced by any entity.
    /// </summary>
    Task<bool> IsReferencedAsync(Guid variableId, CancellationToken cancellationToken = default);
}