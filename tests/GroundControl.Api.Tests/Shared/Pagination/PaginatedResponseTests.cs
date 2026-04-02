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
    public void PaginatedResponse_WithCursorValues_ExposesComputedFlags()
    {
        // Arrange
        var response = new PaginatedResponse<string>
        {
            Data = ["alpha"],
            NextCursor = "next-cursor",
            PreviousCursor = null,
            TotalCount = 42
        };

        // Act
        var hasNext = response.HasNext;
        var hasPrevious = response.HasPrevious;

        // Assert
        hasNext.ShouldBeTrue();
        hasPrevious.ShouldBeFalse();
    }

    [Fact]
    public void Serialize_WithFlattenedPaginationMetadata_ExcludesComputedFlagsFromJson()
    {
        // Arrange
        var response = new PaginatedResponse<string>
        {
            Data = ["alpha", "beta"],
            NextCursor = "next-cursor",
            PreviousCursor = "previous-cursor",
            TotalCount = 2
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
              "nextCursor": "next-cursor",
              "previousCursor": "previous-cursor",
              "totalCount": 2
            }
            """);
    }

    [Fact]
    public void Serialize_WithoutPreviousCursor_OmitsComputedFlagsAndNestedPaginationObject()
    {
        // Arrange
        var response = new PaginatedResponse<string>
        {
            Data = ["alpha", "beta"],
            NextCursor = "next-cursor",
            PreviousCursor = null,
            TotalCount = 2
        };

        // Act
        var json = JsonSerializer.Serialize(response, WebJsonSerializerOptions);

        // Assert
        json.ShouldContain("\"nextCursor\": \"next-cursor\"");
        json.ShouldContain("\"previousCursor\": null");
        json.ShouldContain("\"totalCount\": 2");
        json.ShouldNotContain("\"pagination\"");
        json.ShouldNotContain("\"hasNext\"");
        json.ShouldNotContain("\"hasPrevious\"");
    }
}