namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a client credential for configuration access.
/// </summary>
public class Client
{
    /// <summary>
    /// Gets or sets the unique identifier for the client.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the project identifier granted to the client.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the fixed scope assignments for the client.
    /// </summary>
    public Dictionary<string, string> Scopes { get; init; } = [];

    /// <summary>
    /// Gets or sets the protected client secret.
    /// </summary>
    public required string Secret { get; set; }

    /// <summary>
    /// Gets or sets the human-readable client name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the client is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the optional expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the client was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets the optimistic concurrency version.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the creating user.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the last modification timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the last modifying user.
    /// </summary>
    public Guid UpdatedBy { get; set; }
}