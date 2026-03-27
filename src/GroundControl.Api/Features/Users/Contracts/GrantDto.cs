using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Users.Contracts;

/// <summary>
/// Represents a role grant in request and response contracts.
/// </summary>
internal sealed record GrantDto
{
    /// <summary>
    /// Gets the optional group resource targeted by the grant.
    /// </summary>
    /// <remarks>A <see langword="null" /> value represents a system-wide grant.</remarks>
    public Guid? Resource { get; init; }

    /// <summary>
    /// Gets the identifier of the granted role.
    /// </summary>
    [Required]
    public required Guid RoleId { get; init; }

    /// <summary>
    /// Gets optional scope value conditions that further restrict access.
    /// </summary>
    public Dictionary<string, List<string>>? Conditions { get; init; }

    public Grant ToEntity() => new()
    {
        Resource = Resource,
        RoleId = RoleId,
        Conditions = Conditions ?? []
    };

    public static GrantDto From(Grant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new GrantDto
        {
            Resource = grant.Resource,
            RoleId = grant.RoleId,
            Conditions = grant.Conditions.Count > 0 ? grant.Conditions : null
        };
    }
}