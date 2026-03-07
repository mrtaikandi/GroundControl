namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents an organizational group.
/// </summary>
public class Group
{
    /// <summary>
    /// Gets or sets the unique identifier for the group.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the group.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the group.
    /// </summary>
    public string? Description { get; set; }

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