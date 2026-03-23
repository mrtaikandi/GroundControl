namespace GroundControl.Api.Features.PersonalAccessTokens.Contracts;

internal sealed record CreatePatResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Token { get; init; }

    public required string TokenPrefix { get; init; }

    public IReadOnlyList<string>? Permissions { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}