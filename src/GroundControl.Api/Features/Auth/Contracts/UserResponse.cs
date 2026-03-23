using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Auth.Contracts;

internal sealed record UserResponse
{
    public required Guid Id { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required bool IsActive { get; init; }

    public static UserResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsActive = user.IsActive,
        };
    }
}