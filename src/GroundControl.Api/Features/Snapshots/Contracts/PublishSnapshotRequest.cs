using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Snapshots.Contracts;

/// <summary>
/// Represents the request body for publishing a new snapshot.
/// </summary>
internal sealed record PublishSnapshotRequest
{
    /// <summary>
    /// Gets the optional publication description.
    /// </summary>
    /// <remarks>Maximum length: 500 characters.</remarks>
    [MaxLength(500)]
    public string? Description { get; init; }
}