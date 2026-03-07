namespace GroundControl.Persistence.Contracts;

/// <summary>
/// Represents a paged result set.
/// </summary>
/// <typeparam name="T">The item type contained in the result.</typeparam>
public record PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items returned for the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets or sets the cursor for the next page.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets or sets the total number of matching items.
    /// </summary>
    public required long TotalCount { get; init; }
}