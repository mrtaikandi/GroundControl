using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Roles.Contracts;

/// <summary>
/// Represents the API response body for a role.
/// </summary>
internal sealed record RoleResponse
{
    /// <summary>
    /// Gets the unique identifier for the role.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the display name for the role.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the role.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the permission strings granted by the role.
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the role was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the role.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the role was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the role.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Role" /> entity.
    /// </summary>
    /// <param name="role">The persisted role entity.</param>
    /// <returns>The API response contract.</returns>
    public static RoleResponse From(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            Permissions = [.. role.Permissions],
            Version = role.Version,
            CreatedAt = role.CreatedAt,
            CreatedBy = role.CreatedBy,
            UpdatedAt = role.UpdatedAt,
            UpdatedBy = role.UpdatedBy,
        };
    }
}