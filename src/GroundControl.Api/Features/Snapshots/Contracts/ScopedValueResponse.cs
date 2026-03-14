using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots.Contracts;

/// <summary>
/// Represents a scope-specific value in a snapshot response.
/// </summary>
internal sealed record ScopedValueResponse
{
    /// <summary>
    /// Gets the scope dimension-value pairs that qualify this value.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Scopes { get; init; }

    /// <summary>
    /// Gets the serialized value for the scope combination.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="ScopedValue" />.
    /// </summary>
    public static ScopedValueResponse From(ScopedValue scopedValue)
    {
        ArgumentNullException.ThrowIfNull(scopedValue);

        return new ScopedValueResponse
        {
            Scopes = scopedValue.Scopes,
            Value = scopedValue.Value,
        };
    }
}