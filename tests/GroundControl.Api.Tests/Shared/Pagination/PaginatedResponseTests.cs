using System.Text.Json;
using GroundControl.Api.Shared.Pagination;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Pagination;

public sealed class PaginatedResponseTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void PaginationMetadata_WithCursorValues_ExposesComputedFlags()
    {
        // Arrange
        var metadata = new PaginationMetadata
        {
            NextCursor = "next-cursor",
            PreviousCursor = null,
            TotalCount = 42
        };

        // Act
        var hasNext = metadata.HasNext;
        var hasPrevious = metadata.HasPrevious;

        // Assert
        hasNext.ShouldBeTrue();
        hasPrevious.ShouldBeFalse();
    }

    [Fact]
    public void Serialize_WithPaginationEnvelope_ExcludesComputedFlagsFromJson()
    {
        // Arrange
        var response = new PaginatedResponse<string>
        {
            Data = ["alpha", "beta"],
            Pagination = new PaginationMetadata
            {
                NextCursor = "next-cursor",
                PreviousCursor = "previous-cursor",
                TotalCount = 2
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, WebJsonSerializerOptions);

        // Assert
        json.ShouldBe(
            """
              {
                "data": [
                  "alpha",
                  "beta"
                ],
                "pagination": {
                  "nextCursor": "next-cursor",
                  "previousCursor": "previous-cursor",
                  "totalCount": 2
                }
              }
              """
            );
    }
}