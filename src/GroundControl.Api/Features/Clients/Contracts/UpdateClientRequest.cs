using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Clients.Contracts;

/// <summary>
/// Represents the request body for updating a client.
/// </summary>
internal sealed record UpdateClientRequest
{
    /// <summary>
    /// Gets the client name.
    /// </summary>
    /// <remarks>Maximum length: 200 characters.</remarks>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether the client is active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets the optional expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}