using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for configuration entry entities.
/// </summary>
public interface IConfigEntryStore
{
    /// <summary>
    /// Gets a configuration entry by its unique identifier.
    /// </summary>
    Task<ConfigEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists configuration entries with paging, sorting, and optional filters.
    /// </summary>
    Task<PagedResult<ConfigEntry>> ListAsync(ConfigEntryListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new configuration entry.
    /// </summary>
    Task CreateAsync(ConfigEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing configuration entry with optimistic concurrency.
    /// </summary>
    Task<bool> UpdateAsync(ConfigEntry entry, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration entry with optimistic concurrency.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configuration entries for a specific owner.
    /// </summary>
    Task<IReadOnlyList<ConfigEntry>> GetAllByOwnerAsync(Guid ownerId, ConfigEntryOwnerType ownerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all configuration entries for a specific owner.
    /// </summary>
    Task DeleteAllByOwnerAsync(Guid ownerId, ConfigEntryOwnerType ownerType, CancellationToken cancellationToken = default);
}