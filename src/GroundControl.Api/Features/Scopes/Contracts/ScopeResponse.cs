using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Scopes.Contracts;

/// <summary>
/// Represents the API response body for a scope definition.
/// </summary>
internal sealed record ScopeResponse
{
    /// <summary>
    /// Gets the unique identifier for the scope.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the unique scope dimension name.
    /// </summary>
    public required string Dimension { get; init; }

    /// <summary>
    /// Gets the allowed values for the scope dimension.
    /// </summary>
    public required IReadOnlyList<string> AllowedValues { get; init; }

    /// <summary>
    /// Gets the optional human-readable description for the scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the timestamp when the scope was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that created the scope.
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets the timestamp when the scope was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the identifier of the user that last updated the scope.
    /// </summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="Scope" /> entity.
    /// </summary>
    /// <param name="scope">The persisted scope entity.</param>
    /// <returns>The API response contract.</returns>
    public static ScopeResponse From(Scope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return new ScopeResponse
        {
            Id = scope.Id,
            Dimension = scope.Dimension,
            AllowedValues = [.. scope.AllowedValues],
            Description = scope.Description,
            Version = scope.Version,
            CreatedAt = scope.CreatedAt,
            CreatedBy = scope.CreatedBy,
            UpdatedAt = scope.UpdatedAt,
            UpdatedBy = scope.UpdatedBy,
        };
    }
}