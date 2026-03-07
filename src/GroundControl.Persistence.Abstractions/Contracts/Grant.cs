namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a role grant assigned to a user.
/// </summary>
public class Grant
{
    /// <summary>
    /// Gets or sets the group resource targeted by the grant.
    /// </summary>
    /// <remarks>
    /// A <see langword="null" /> value represents a system-wide grant.
    /// </remarks>
    public Guid? Resource { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the granted role.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Gets or sets optional scope value conditions that further restrict access.
    /// </summary>
    public Dictionary<string, List<string>> Conditions { get; init; } = [];
}