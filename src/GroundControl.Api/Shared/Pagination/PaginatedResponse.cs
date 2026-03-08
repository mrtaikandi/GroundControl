namespace GroundControl.Api.Shared.Pagination;

/// <summary>
/// Represents a paginated response envelope for list endpoints.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
internal sealed record PaginatedResponse<T>
{
    /// <summary>
    /// Gets the items in the current page.
    /// </summary>
    public required IReadOnlyList<T> Data { get; init; }

    /// <summary>
    /// Gets the cursor for the next page.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the cursor for the previous page.
    /// </summary>
    public string? PreviousCursor { get; init; }

    /// <summary>
    /// Gets a value indicating whether another page exists after the current page.
    /// </summary>
    [JsonIgnore]
    public bool HasNext => NextCursor is not null;

    /// <summary>
    /// Gets a value indicating whether another page exists before the current page.
    /// </summary>
    [JsonIgnore]
    public bool HasPrevious => PreviousCursor is not null;

    /// <summary>
    /// Gets the total number of matching items.
    /// </summary>
    public required long TotalCount { get; init; }
}