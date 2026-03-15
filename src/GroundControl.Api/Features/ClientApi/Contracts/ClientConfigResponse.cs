namespace GroundControl.Api.Features.ClientApi.Contracts;

/// <summary>
/// Represents the resolved configuration payload returned to an authenticated client.
/// </summary>
internal sealed record ClientConfigResponse
{
    /// <summary>
    /// Gets the flat key-value configuration data resolved for the client's scopes.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Data { get; init; }

    /// <summary>
    /// Gets the unique identifier of the active snapshot.
    /// </summary>
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Gets the version of the active snapshot, used as the ETag value.
    /// </summary>
    public required long SnapshotVersion { get; init; }
}