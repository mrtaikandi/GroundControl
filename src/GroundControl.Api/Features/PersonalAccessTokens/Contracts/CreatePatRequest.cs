using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.PersonalAccessTokens.Contracts;

internal sealed record CreatePatRequest
{
    /// <summary>
    /// Gets the human-readable name for the token.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the number of days until the token expires.
    /// </summary>
    [Range(1, 365)]
    public int? ExpiresInDays { get; init; }

    /// <summary>
    /// Gets the optional permission whitelist for the token.
    /// </summary>
    public IReadOnlyList<string>? Permissions { get; init; }
}