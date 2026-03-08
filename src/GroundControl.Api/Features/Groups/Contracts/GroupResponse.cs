using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Groups.Contracts;

/// <summary>
/// Represents the API response body for a group.
/// </summary>
internal sealed record GroupResponse
{
    /// <summary>
    /// Gets the unique identifier for the group.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the display name for the group.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the group.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the group was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the group.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the group was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the group.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Group" /> entity.
    /// </summary>
    /// <param name="group">The persisted group entity.</param>
    /// <returns>The API response contract.</returns>
    public static GroupResponse From(Group group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new GroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Version = group.Version,
            CreatedAt = group.CreatedAt,
            CreatedBy = group.CreatedBy,
            UpdatedAt = group.UpdatedAt,
            UpdatedBy = group.UpdatedBy,
        };
    }
}