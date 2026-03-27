using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.ConfigEntries.Contracts;

/// <summary>
/// Represents the API response body for a configuration entry.
/// </summary>
internal sealed record ConfigEntryResponse
{
    /// <summary>
    /// Gets the unique identifier for the configuration entry.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the configuration key. A key can use a colon-separated hierarchical format
    /// (e.g., <c>Logging:LogLevel:Default</c>, <c>Database:ConnectionString</c>)
    /// </summary>
    /// <remarks>
    /// A key must be unique within its owner (the combination
    /// of <see cref="OwnerId"/> and <see cref="OwnerType"/>). The same key may exist on both a
    /// template and a project; during snapshot resolution the project entry overrides the template
    /// entry. Values associated with a key may contain <c>{{variableName}}</c> placeholders that
    /// are resolved at snapshot publish time.
    /// </remarks>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the owning template or project identifier.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Gets the owner type.
    /// </summary>
    public required ConfigEntryOwnerType OwnerType { get; init; }

    /// <summary>
    /// Gets the value type name.
    /// </summary>
    public required string ValueType { get; init; }

    /// <summary>
    /// Gets the scope-specific values.
    /// </summary>
    public required IReadOnlyCollection<ScopedValue> Values { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry contains sensitive data.
    /// </summary>
    public required bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the optional entry description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the entry was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the entry.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the entry was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the entry.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="ConfigEntry" /> entity.
    /// </summary>
    /// <param name="entry">The persisted configuration entry entity.</param>
    /// <param name="maskedValues">Optional pre-masked values to use instead of the entity's raw values.</param>
    /// <returns>The API response contract.</returns>
    public static ConfigEntryResponse From(ConfigEntry entry, IReadOnlyCollection<ScopedValue>? maskedValues = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ConfigEntryResponse
        {
            Id = entry.Id,
            Key = entry.Key,
            OwnerId = entry.OwnerId,
            OwnerType = entry.OwnerType,
            ValueType = entry.ValueType,
            Values = maskedValues ?? entry.Values,
            IsSensitive = entry.IsSensitive,
            Description = entry.Description,
            Version = entry.Version,
            CreatedAt = entry.CreatedAt,
            CreatedBy = entry.CreatedBy,
            UpdatedAt = entry.UpdatedAt,
            UpdatedBy = entry.UpdatedBy,
        };
    }
}