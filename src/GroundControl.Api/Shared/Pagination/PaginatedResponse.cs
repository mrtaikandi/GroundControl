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
    /// Gets the pagination metadata for the current page.
    /// </summary>
    public required PaginationMetadata Pagination { get; init; }
}