namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a configured scope dimension.
/// </summary>
public class Scope
{
    /// <summary>
    /// Gets or sets the unique identifier for the scope.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the scope dimension name.
    /// </summary>
    public required string Dimension { get; set; }

    /// <summary>
    /// Gets or sets the allowed values for the dimension.
    /// </summary>
    public IList<string> AllowedValues { get; init; } = [];

    /// <summary>
    /// Gets or sets the optional human-readable description.
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