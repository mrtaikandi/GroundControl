using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.ConfigEntries.Contracts;

/// <summary>
/// Represents the request body for creating a configuration entry.
/// </summary>
internal sealed record CreateConfigEntryRequest
{
    /// <summary>
    /// Gets the configuration key. Must start with a letter and contain only letters, digits, or
    /// the separators <c>.</c>, <c>:</c>, <c>_</c>, <c>-</c>.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [Required]
    [MaxLength(500)]
    [RegularExpression(ConfigEntryValidation.KeyPattern, ErrorMessage = ConfigEntryValidation.KeyPatternErrorMessage)]
    public required string Key { get; init; }

    /// <summary>
    /// Gets the owning template or project identifier.
    /// </summary>
    [Required]
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Gets the owner type.
    /// </summary>
    [Required]
    public required ConfigEntryOwnerType OwnerType { get; init; }

    /// <summary>
    /// Gets the value type name.
    /// </summary>
    /// <remarks>Maximum length: 50 characters.</remarks>
    [Required]
    [MaxLength(50)]
    public required string ValueType { get; init; }

    /// <summary>
    /// Gets the scope-specific values.
    /// </summary>
    [Required]
    public required List<ScopedValueRequest> Values { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the optional entry description.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }
}