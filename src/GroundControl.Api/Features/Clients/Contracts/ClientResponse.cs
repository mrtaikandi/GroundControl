using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Clients.Contracts;

/// <summary>
/// Represents the API response body for a client.
/// </summary>
internal sealed record ClientResponse
{
    /// <summary>
    /// Gets the unique identifier for the client.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the project identifier the client belongs to.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the client name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the fixed scope assignments for the client.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Scopes { get; init; }

    /// <summary>
    /// Gets whether the client is active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets the optional expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the timestamp when the client was last used.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the client was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the client.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the client was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the client.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Client" /> entity.
    /// </summary>
    /// <param name="client">The persisted client entity.</param>
    /// <returns>The API response contract.</returns>
    public static ClientResponse From(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new ClientResponse
        {
            Id = client.Id,
            ProjectId = client.ProjectId,
            Name = client.Name,
            Scopes = client.Scopes.AsReadOnly(),
            IsActive = client.IsActive,
            ExpiresAt = client.ExpiresAt,
            LastUsedAt = client.LastUsedAt,
            Version = client.Version,
            CreatedAt = client.CreatedAt,
            CreatedBy = client.CreatedBy,
            UpdatedAt = client.UpdatedAt,
            UpdatedBy = client.UpdatedBy,
        };
    }
}