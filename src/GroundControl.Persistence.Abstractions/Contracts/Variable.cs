namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a named interpolation variable.
/// </summary>
public class Variable
{
    /// <summary>
    /// Gets or sets the unique identifier for the variable.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional variable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the variable scope tier.
    /// </summary>
    public VariableScope Scope { get; set; }

    /// <summary>
    /// Gets or sets the owning group identifier for global variables.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the owning project identifier for project variables.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the scope-specific variable values.
    /// </summary>
    public IList<ScopedValue> Values { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the variable contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; set; }

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