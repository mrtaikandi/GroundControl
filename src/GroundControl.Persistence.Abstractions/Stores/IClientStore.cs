using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for client entities.
/// </summary>
public interface IClientStore
{
    /// <summary>
    /// Gets a client by its unique identifier.
    /// </summary>
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists clients for a project with paging and sorting.
    /// </summary>
    Task<PagedResult<Client>> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new client.
    /// </summary>
    Task CreateAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing client with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(Client client, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a client with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last-used timestamp for a client.
    /// </summary>
    Task UpdateLastUsedAsync(Guid id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all clients belonging to a project.
    /// </summary>
    Task DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets clients that are expired and deactivated beyond the grace period.
    /// </summary>
    Task<IReadOnlyList<Client>> GetExpiredAndDeactivatedAsync(int gracePeriodDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a client record.
    /// </summary>
    Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}