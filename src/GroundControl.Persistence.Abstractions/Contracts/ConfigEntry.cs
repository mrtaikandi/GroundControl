namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a configuration entry owned by a template or project.
/// </summary>
public class ConfigEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the configuration entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the configuration key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the owning template or project identifier.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the owner type.
    /// </summary>
    public ConfigEntryOwnerType OwnerType { get; set; }

    /// <summary>
    /// Gets or sets the serialized value type name.
    /// </summary>
    public required string ValueType { get; set; }

    /// <summary>
    /// Gets or sets the scope-specific values.
    /// </summary>
    public HashSet<ScopedValue> Values { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the entry contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets the optional entry description.
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