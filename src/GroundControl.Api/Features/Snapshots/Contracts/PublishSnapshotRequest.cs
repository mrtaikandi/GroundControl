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

    /// <summary>
    /// Gets the optional diff hash returned by a prior preview call. When supplied, the publish call
    /// fails with 409 if the resolved configuration's hash differs at publish time, indicating that
    /// the project was mutated since the preview was generated.
    /// </summary>
    [MaxLength(128)]
    public string? ExpectedHash { get; init; }
}