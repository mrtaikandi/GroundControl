using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Users.Contracts;

/// <summary>
/// Represents the request body for updating a user.
/// </summary>
internal sealed record UpdateUserRequest
{
    /// <summary>
    /// Gets the username.
    /// </summary>
    /// <remarks>Maximum length: 100 characters.</remarks>
    [Required]
    [MaxLength(100)]
    public required string Username { get; init; }

    /// <summary>
    /// Gets the email address.
    /// </summary>
    /// <remarks>Maximum length: 254 characters.</remarks>
    [Required]
    [MaxLength(254)]
    [EmailAddress]
    public required string Email { get; init; }

    /// <summary>
    /// Gets the role grants to assign. Requires <c>users:write</c> permission.
    /// </summary>
    public IReadOnlyList<GrantDto>? Grants { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user should be active. Requires <c>users:write</c> permission.
    /// </summary>
    public bool? IsActive { get; init; }
}