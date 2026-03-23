using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Users.Contracts;

/// <summary>
/// Represents the API response body for a user.
/// </summary>
internal sealed record UserResponse
{
    /// <summary>
    /// Gets the unique identifier for the user.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Gets the email address.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Gets the role grants assigned to the user.
    /// </summary>
    public required IReadOnlyList<GrantDto> Grants { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user is active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets the external identity provider name.
    /// </summary>
    public string? ExternalProvider { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the user was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created this user.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the user was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated this user.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="User" /> entity.
    /// </summary>
    /// <param name="user">The persisted user entity.</param>
    /// <returns>The API response contract.</returns>
    public static UserResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Grants = user.Grants.Select(GrantDto.From).ToList(),
            IsActive = user.IsActive,
            ExternalProvider = user.ExternalProvider,
            Version = user.Version,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            UpdatedAt = user.UpdatedAt,
            UpdatedBy = user.UpdatedBy,
        };
    }
}