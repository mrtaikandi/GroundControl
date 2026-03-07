namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a reusable configuration template.
/// </summary>
public class Template
{
    /// <summary>
    /// Gets or sets the unique identifier for the template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional template description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the owning group identifier.
    /// </summary>
    public Guid? GroupId { get; set; }

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