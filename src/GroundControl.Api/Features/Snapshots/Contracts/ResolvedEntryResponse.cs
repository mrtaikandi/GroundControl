using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Snapshots.Contracts;

/// <summary>
/// Represents a resolved configuration entry in a snapshot response.
/// </summary>
internal sealed record ResolvedEntryResponse
{
    /// <summary>
    /// Gets the configuration key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the serialized value type name.
    /// </summary>
    public required string ValueType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry contains sensitive data.
    /// </summary>
    public required bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the resolved values for each applicable scope combination.
    /// </summary>
    public required IReadOnlyList<ScopedValueResponse> Values { get; init; }

    /// <summary>
    /// Creates a response contract from a persisted <see cref="ResolvedEntry" />.
    /// </summary>
    public static ResolvedEntryResponse From(ResolvedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ResolvedEntryResponse
        {
            Key = entry.Key,
            ValueType = entry.ValueType,
            IsSensitive = entry.IsSensitive,
            Values = entry.Values.Select(ScopedValueResponse.From).ToList(),
        };
    }
}