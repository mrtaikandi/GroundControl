using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for personal access token entities.
/// </summary>
public interface IPersonalAccessTokenStore
{
    /// <summary>
    /// Gets a personal access token by its unique identifier.
    /// </summary>
    Task<PersonalAccessToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a personal access token by its hash.
    /// </summary>
    Task<PersonalAccessToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all personal access tokens for a user.
    /// </summary>
    Task<IReadOnlyList<PersonalAccessToken>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new personal access token.
    /// </summary>
    Task CreateAsync(PersonalAccessToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a personal access token.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last-used timestamp for a personal access token.
    /// </summary>
    Task UpdateLastUsedAsync(Guid id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active (non-revoked, non-expired) tokens for a user.
    /// </summary>
    Task<int> GetActiveCountByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}