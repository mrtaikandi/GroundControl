using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for role entities.
/// </summary>
public interface IRoleStore
{
    /// <summary>
    /// Gets a role by its unique identifier.
    /// </summary>
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a role by its name.
    /// </summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all roles.
    /// </summary>
    Task<IReadOnlyList<Role>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new role.
    /// </summary>
    Task CreateAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing role with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Role role, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the role is referenced by any users.
    /// </summary>
    Task<bool> IsReferencedByUsersAsync(Guid roleId, CancellationToken cancellationToken = default);
}