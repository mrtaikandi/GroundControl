using GroundControl.Persistence.Contracts;

namespace GroundControl.Persistence.Stores;

/// <summary>
/// Data access contract for refresh token entities.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Gets a refresh token by its hash.
    /// </summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new refresh token.
    /// </summary>
    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token, optionally recording its replacement.
    /// </summary>
    Task RevokeAsync(Guid id, DateTimeOffset revokedAt, Guid? replacedByTokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all refresh tokens in a token family.
    /// </summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all refresh tokens for a user.
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}