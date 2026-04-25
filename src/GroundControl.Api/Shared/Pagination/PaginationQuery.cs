using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Shared.Pagination;

/// <summary>
/// Represents the query parameters for cursor-based pagination requests.
/// </summary>
internal class PaginationQuery
{
    /// <summary>
    /// Gets the default number of items to return when the client does not specify a limit. The default is 25.
    /// </summary>
    public const int DefaultLimit = 25;

    /// <summary>
    /// Gets the default field name used for sorting when the client does not specify one.
    /// </summary>
    public const string DefaultSortField = "name";

    /// <summary>
    /// Gets the default sort direction when the client does not specify one.
    /// </summary>
    public const string DefaultSortOrder = "asc";

    /// <summary>
    /// Gets the cursor pointing to the item after which results should begin (forward pagination).
    /// </summary>
    public string? After { get; init; }

    /// <summary>
    /// Gets the cursor pointing to the item before which results should end (backward pagination).
    /// </summary>
    public string? Before { get; init; }

    /// <summary>
    /// Gets the maximum number of items to return. Must be between 1 and 100.
    /// </summary>
    [Range(1, 100)]
    public int? Limit { get; init; } = DefaultLimit;

    /// <summary>
    /// Gets the name of the field to sort results by.
    /// </summary>
    public string? SortField { get; init; }

    /// <summary>
    /// Gets the sort direction (e.g., <c>asc</c> or <c>desc</c>).
    /// </summary>
    public string? SortOrder { get; init; }
}