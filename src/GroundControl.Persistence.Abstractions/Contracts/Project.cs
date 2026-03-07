namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a configuration project.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the unique identifier for the project.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the owning group identifier.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the ordered template identifiers applied to the project.
    /// </summary>
    public IList<Guid> TemplateIds { get; init; } = [];

    /// <summary>
    /// Gets or sets the active snapshot identifier.
    /// </summary>
    public Guid? ActiveSnapshotId { get; set; }

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