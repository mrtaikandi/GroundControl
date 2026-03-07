using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for user entities.
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// Gets a user by its unique identifier.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by username.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by external identity provider and external ID.
    /// </summary>
    Task<User?> GetByExternalIdAsync(string externalProvider, string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users with paging and sorting.
    /// </summary>
    Task<PagedResult<User>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(User user, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users belonging to a group.
    /// </summary>
    Task<IReadOnlyList<User>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
}