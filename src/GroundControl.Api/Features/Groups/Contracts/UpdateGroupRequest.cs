using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Groups.Contracts;

/// <summary>
/// Represents the request body for updating a group.
/// </summary>
internal sealed record UpdateGroupRequest
{
    /// <summary>
    /// Gets the display name for the group.
    /// </summary>
    /// <remarks>Maximum length: 100 characters.</remarks>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the group.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }
}