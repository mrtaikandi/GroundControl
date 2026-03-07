namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a query for listing configuration entries.
/// </summary>
public class ConfigEntryListQuery : ListQuery
{
    /// <summary>
    /// Gets or sets the owner identifier filter.
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the owner type filter.
    /// </summary>
    public ConfigEntryOwnerType? OwnerType { get; set; }

    /// <summary>
    /// Gets or sets the configuration key prefix filter.
    /// </summary>
    public string? KeyPrefix { get; set; }
}