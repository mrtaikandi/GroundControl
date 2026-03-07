namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents an immutable snapshot of resolved project configuration.
/// </summary>
public class Snapshot
{
    /// <summary>
    /// Gets or sets the unique identifier for the snapshot.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the project identifier that owns the snapshot.
    /// </summary>
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets or sets the monotonically increasing snapshot version within the project.
    /// </summary>
    public long SnapshotVersion { get; init; }

    /// <summary>
    /// Gets or sets the resolved entries captured in the snapshot.
    /// </summary>
    public IList<ResolvedEntry> Entries { get; init; } = [];

    /// <summary>
    /// Gets or sets the publication timestamp.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the publishing user.
    /// </summary>
    public Guid PublishedBy { get; init; }

    /// <summary>
    /// Gets or sets the optional publication description.
    /// </summary>
    public string? Description { get; init; }
}