using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Users.Contracts;

/// <summary>
/// Represents the request body for creating a user.
/// </summary>
internal sealed record CreateUserRequest
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
    /// Gets the initial password (required in BuiltIn authentication mode).
    /// </summary>
    [MaxLength(128)]
    public string? Password { get; init; }

    /// <summary>
    /// Gets the role grants to assign to the new user.
    /// </summary>
    public IReadOnlyList<GrantDto>? Grants { get; init; }
}