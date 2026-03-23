using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.PersonalAccessTokens.Contracts;

internal sealed record PatResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string TokenPrefix { get; init; }

    public IReadOnlyList<string>? Permissions { get; init; }

    public bool IsRevoked { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static PatResponse From(PersonalAccessToken pat)
    {
        ArgumentNullException.ThrowIfNull(pat);

        return new PatResponse
        {
            Id = pat.Id,
            Name = pat.Name,
            TokenPrefix = pat.TokenPrefix,
            Permissions = pat.Permissions is not null ? [.. pat.Permissions] : null,
            IsRevoked = pat.IsRevoked,
            ExpiresAt = pat.ExpiresAt,
            LastUsedAt = pat.LastUsedAt,
            CreatedAt = pat.CreatedAt,
        };
    }
}