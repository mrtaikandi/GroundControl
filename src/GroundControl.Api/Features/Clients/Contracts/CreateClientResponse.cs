using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Clients.Contracts;

/// <summary>
/// Represents the API response body for a newly created client, including the raw secret.
/// </summary>
internal sealed record CreateClientResponse
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
    /// Gets the raw client secret. This is only returned at creation time.
    /// </summary>
    public required string ClientSecret { get; init; }

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
    /// Creates a creation response from a persisted <see cref="Client" /> entity and the raw secret.
    /// </summary>
    /// <param name="client">The persisted client entity.</param>
    /// <param name="rawSecret">The raw client secret (before encryption).</param>
    /// <returns>The API response contract including the secret.</returns>
    public static CreateClientResponse From(Client client, string rawSecret)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);

        return new CreateClientResponse
        {
            Id = client.Id,
            ProjectId = client.ProjectId,
            Name = client.Name,
            Scopes = client.Scopes.AsReadOnly(),
            IsActive = client.IsActive,
            ExpiresAt = client.ExpiresAt,
            ClientSecret = rawSecret,
            Version = client.Version,
            CreatedAt = client.CreatedAt,
            CreatedBy = client.CreatedBy,
        };
    }
}