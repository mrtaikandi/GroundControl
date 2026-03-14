using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots.Contracts;

/// <summary>
/// Represents the full API response body for a snapshot, including resolved entries.
/// </summary>
internal sealed record SnapshotResponse
{
    /// <summary>
    /// Gets the unique identifier for the snapshot.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the project identifier that owns the snapshot.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the monotonically increasing snapshot version within the project.
    /// </summary>
    public required long SnapshotVersion { get; init; }

    /// <summary>
    /// Gets the resolved entries captured in the snapshot.
    /// </summary>
    public required IReadOnlyList<ResolvedEntryResponse> Entries { get; init; }

    /// <summary>
    /// Gets the publication timestamp.
    /// </summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the publishing user.
    /// </summary>
    public required Guid PublishedBy { get; init; }

    /// <summary>
    /// Gets the optional publication description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Snapshot" /> entity.
    /// </summary>
    public static SnapshotResponse From(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SnapshotResponse
        {
            Id = snapshot.Id,
            ProjectId = snapshot.ProjectId,
            SnapshotVersion = snapshot.SnapshotVersion,
            Entries = snapshot.Entries.Select(ResolvedEntryResponse.From).ToList(),
            PublishedAt = snapshot.PublishedAt,
            PublishedBy = snapshot.PublishedBy,
            Description = snapshot.Description,
        };
    }
}