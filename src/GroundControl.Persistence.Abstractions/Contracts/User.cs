namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents an administrative user.
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the role grants assigned to the user.
    /// </summary>
    public IList<Grant> Grants { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the external identity subject identifier.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the external identity provider name.
    /// </summary>
    public string? ExternalProvider { get; set; }

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