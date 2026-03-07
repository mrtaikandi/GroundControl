namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a named permission bundle.
/// </summary>
public class Role
{
    /// <summary>
    /// Gets or sets the unique identifier for the role.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the permission strings granted by the role.
    /// </summary>
    public IList<string> Permissions { get; init; } = [];

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