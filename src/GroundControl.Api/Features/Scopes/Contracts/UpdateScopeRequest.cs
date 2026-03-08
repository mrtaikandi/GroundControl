using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Scopes.Contracts;

/// <summary>
/// Represents the request body for updating a scope definition.
/// </summary>
internal sealed record UpdateScopeRequest
{
    /// <summary>
    /// Gets the unique scope dimension name.
    /// </summary>
    /// <remarks>Maximum length: 100 characters.</remarks>
    [Required]
    [MaxLength(100)]
    public required string Dimension { get; init; }

    /// <summary>
    /// Gets the allowed values for the scope dimension.
    /// </summary>
    /// <remarks>At least one value is required.</remarks>
    [Required]
    [MinLength(1)]
    public required List<string> AllowedValues { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the scope.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }
}