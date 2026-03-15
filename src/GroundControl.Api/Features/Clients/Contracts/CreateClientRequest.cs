using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Clients.Contracts;

/// <summary>
/// Represents the request body for creating a client.
/// </summary>
internal sealed record CreateClientRequest
{
    /// <summary>
    /// Gets the client name.
    /// </summary>
    /// <remarks>Maximum length: 200 characters.</remarks>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the fixed scope assignments for the client.
    /// </summary>
    public Dictionary<string, string>? Scopes { get; init; }

    /// <summary>
    /// Gets the optional expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}