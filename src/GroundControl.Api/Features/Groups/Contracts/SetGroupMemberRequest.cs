using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Groups.Contracts;

/// <summary>
/// Represents the request body for adding a user as a group member with a specific role.
/// </summary>
internal sealed record SetGroupMemberRequest
{
    /// <summary>
    /// Gets the identifier of the role to grant for this group membership.
    /// </summary>
    [Required]
    public required Guid RoleId { get; init; }
}