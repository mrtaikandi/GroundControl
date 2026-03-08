using System.Text.Json.Serialization;

namespace GroundControl.Api.Shared.Pagination;

internal sealed record PaginationMetadata
{
    public string? NextCursor { get; init; }

    public string? PreviousCursor { get; init; }

    [JsonIgnore]
    public bool HasNext => NextCursor is not null;

    [JsonIgnore]
    public bool HasPrevious => PreviousCursor is not null;

    public required long TotalCount { get; init; }
}