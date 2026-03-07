namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a refresh token used for token rotation.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Gets or sets the unique identifier for the refresh token.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the token family identifier.
    /// </summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the refresh token.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the revocation timestamp.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the replacement token.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
}