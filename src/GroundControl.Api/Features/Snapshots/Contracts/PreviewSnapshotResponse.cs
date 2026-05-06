namespace GroundControl.Api.Features.Snapshots.Contracts;

/// <summary>
/// Represents the API response body for a snapshot preview. Mirrors the shape of a published
/// snapshot so callers can diff the preview's <see cref="Entries"/> against an active snapshot's
/// entries directly.
/// </summary>
internal sealed record PreviewSnapshotResponse
{
    /// <summary>
    /// Gets the project identifier the preview was computed against.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the snapshot version that would be assigned if a publish call were made now.
    /// </summary>
    public required long NextVersion { get; init; }

    /// <summary>
    /// Gets the BSON size of the would-be snapshot in bytes. Surfaces the same 16MB limit that
    /// publish would enforce so callers can fail fast.
    /// </summary>
    public required long BsonSizeBytes { get; init; }

    /// <summary>
    /// Gets a deterministic SHA-256 hex digest over the preview's resolved entries. Pass back to
    /// the publish endpoint as <c>expectedHash</c> to detect drift between preview and publish.
    /// </summary>
    public required string DiffHash { get; init; }

    /// <summary>
    /// Gets the resolved entries that would be written to the snapshot, masking sensitive values
    /// unless the caller has the <c>SensitiveValuesDecrypt</c> permission and supplied
    /// <c>?decrypt=true</c>.
    /// </summary>
    public required IReadOnlyList<ResolvedEntryResponse> Entries { get; init; }
}
