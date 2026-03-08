using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.ConfigEntries.Contracts;

/// <summary>
/// Represents the request body for updating a configuration entry.
/// </summary>
internal sealed record UpdateConfigEntryRequest
{
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