namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a personal access token issued to a user.
/// </summary>
public class PersonalAccessToken
{
    /// <summary>
    /// Gets or sets the unique identifier for the token.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable token name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the display prefix of the raw token.
    /// </summary>
    public required string TokenPrefix { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the raw token.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the optional expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the token was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Gets or sets the optional permission whitelist for the token.
    /// </summary>
    public IList<string>? Permissions { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}