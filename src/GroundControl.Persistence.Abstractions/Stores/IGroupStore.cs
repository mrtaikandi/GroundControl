using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for group entities.
/// </summary>
public interface IGroupStore
{
    /// <summary>
    /// Gets a group by its unique identifier.
    /// </summary>
    Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a group by its name.
    /// </summary>
    Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists groups with paging and sorting.
    /// </summary>
    Task<PagedResult<Group>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new group.
    /// </summary>
    Task CreateAsync(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing group with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Group group, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a group with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the group has dependent entities.
    /// </summary>
    Task<bool> HasDependentsAsync(Guid groupId, CancellationToken cancellationToken = default);
}