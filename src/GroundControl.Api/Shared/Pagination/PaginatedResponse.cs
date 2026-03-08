using System.Text.Json.Serialization;

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
    /// Gets the cursor for the next page, or <c>null</c> if there are no more pages.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the total number of items matching the query.
    /// </summary>
    public required long TotalCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more pages are available.
    /// </summary>
    [JsonIgnore]
    public bool HasMore => NextCursor is not null;
}