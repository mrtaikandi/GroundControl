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
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        // Assert
        data.GetArrayLength().ShouldBe(2);
        data[0].GetString().ShouldBe("alpha");
        data[1].GetString().ShouldBe("beta");
        root.GetProperty("nextCursor").GetString().ShouldBe("next-cursor");
        root.GetProperty("previousCursor").GetString().ShouldBe("previous-cursor");
        root.GetProperty("totalCount").GetInt64().ShouldBe(2);
        root.TryGetProperty("pagination", out _).ShouldBeFalse();
        root.TryGetProperty("hasNext", out _).ShouldBeFalse();
        root.TryGetProperty("hasPrevious", out _).ShouldBeFalse();
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