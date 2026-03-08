using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.ConfigEntries.Contracts;

/// <summary>
/// Represents a scope-specific value in a configuration entry request.
/// </summary>
internal sealed record ScopedValueRequest
{
    /// <summary>
    /// Gets the scope dimension-value pairs that qualify this value.
    /// </summary>
    public Dictionary<string, string> Scopes { get; init; } = [];

    /// <summary>
    /// Gets the serialized value for the scope combination.
    /// </summary>
    [Required]
    public required string Value { get; init; }
}