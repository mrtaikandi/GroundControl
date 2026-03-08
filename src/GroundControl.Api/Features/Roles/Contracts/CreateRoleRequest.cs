using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Roles.Contracts;

/// <summary>
/// Represents the request body for creating a role.
/// </summary>
internal sealed record CreateRoleRequest
{
    /// <summary>
    /// Gets the display name for the role.
    /// </summary>
    /// <remarks>Maximum length: 100 characters.</remarks>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the role.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the permission strings granted by the role.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required IReadOnlyList<string> Permissions { get; init; }
}