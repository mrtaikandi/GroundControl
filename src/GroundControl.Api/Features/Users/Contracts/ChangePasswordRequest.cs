using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Users.Contracts;

/// <summary>
/// Represents the request body for changing a user's password.
/// </summary>
internal sealed record ChangePasswordRequest
{
    /// <summary>
    /// Gets the current password.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string CurrentPassword { get; init; }

    /// <summary>
    /// Gets the new password.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string NewPassword { get; init; }
}