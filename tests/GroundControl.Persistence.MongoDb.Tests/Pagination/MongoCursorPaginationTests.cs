using System.ComponentModel.DataAnnotations;
using System.Text;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Pagination;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Pagination;

public sealed class MongoCursorPaginationTests
{
    static MongoCursorPaginationTests()
    {
        MongoConventions.Register();
    }

    public static TheoryData<object?> SupportedSortValues =>
    [
        "environment",
        true,
        42,
        99L,
        18446744073709551615UL,
        123.45m,
        1.5d,
        Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6642"),
        new DateTime(2026, 03, 08, 12, 30, 45, DateTimeKind.Utc),
        new DateTimeOffset(2026, 03, 08, 12, 30, 45, TimeSpan.Zero),
        (object?)null
    ];

    [Theory]
    [MemberData(nameof(SupportedSortValues))]
    public void EncodeDecode_WithSupportedSortValue_RoundTrips(object? sortValue)
    {
        // Arrange
        var cursor = new PagingCursor
        {
            Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6641"),
            SortField = "name",
            SortOrder = "asc",
            SortValue = sortValue
        };

        // Act
        var encoded = MongoCursorPagination.Encode(cursor);
        var success = MongoCursorPagination.TryDecode(encoded, out var decoded, out var errorMessage);

        // Assert
        success.ShouldBeTrue(errorMessage);
        decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe(cursor.Id);
        decoded.SortField.ShouldBe(cursor.SortField);
        decoded.SortOrder.ShouldBe(cursor.SortOrder);
        decoded.Version.ShouldBe(cursor.Version);
        decoded.SortValue.ShouldBe(sortValue);
        decoded.SortValue?.GetType().ShouldBe(sortValue?.GetType());
    }

    [Fact]
    public void TryDecode_WithInvalidBase64_ReturnsFalse()
    {
        // Arrange
        const string Cursor = "not-base64";

        // Act
        var success = MongoCursorPagination.TryDecode(Cursor, out var decoded, out var errorMessage);

        // Assert
        success.ShouldBeFalse();
        decoded.ShouldBeNull();
        errorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("Base64");
    }

    [Fact]
    public void TryDecode_WithUnsupportedTypeCode_ReturnsFalse()
    {
        // Arrange
        const string Payload = """{"version":1,"id":"01957d44-f548-7f0a-91f0-31c0e2df6641","sortField":"name","sortOrder":"asc","sortValue":{"type":"type","value":"System.String"}}""";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(Payload));

        // Act
        var success = MongoCursorPagination.TryDecode(cursor, out var decoded, out var errorMessage);

        // Assert
        success.ShouldBeFalse();
        decoded.ShouldBeNull();
        errorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("not supported");
    }

    [Fact]
    public void BuildPageFilter_WithoutCursor_ReturnsEmptyFilter()
    {
        // Arrange
        var query = new ListQuery();

        // Act
        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, "dimension");
        var rendered = RenderFilter(filter);

        // Assert
        rendered.ElementCount.ShouldBe(0);
    }

    [Fact]
    public void BuildPageFilter_WithAscendingAfterQuery_UsesGreaterThanTieBreaker()
    {
        // Arrange
        var id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6643");
        var query = new ListQuery
        {
            After = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = id,
                SortField = "name",
                SortOrder = "asc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        // Act
        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, "dimension");
        var rendered = RenderFilter(filter);

        // Assert
        rendered.ShouldBe(new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("dimension", new BsonDocument("$gt", "environment")),
            new BsonDocument
            {
                { "dimension", "environment" },
                { "_id", new BsonDocument("$gt", new BsonBinaryData(id, GuidRepresentation.Standard)) }
            }
        }));
    }

    [Fact]
    public void BuildPageFilter_WithAscendingBeforeQuery_UsesLessThanTieBreaker()
    {
        // Arrange
        var id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6644");
        var query = new ListQuery
        {
            Before = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = id,
                SortField = "name",
                SortOrder = "asc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        // Act
        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, "dimension");
        var rendered = RenderFilter(filter);

        // Assert
        rendered.ShouldBe(new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("dimension", new BsonDocument("$lt", "environment")),
            new BsonDocument
            {
                { "dimension", "environment" },
                { "_id", new BsonDocument("$lt", new BsonBinaryData(id, GuidRepresentation.Standard)) }
            }
        }));
    }

    [Fact]
    public void BuildPageFilter_WithDescendingAfterQuery_UsesLessThanTieBreaker()
    {
        // Arrange
        var id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6645");
        var query = new ListQuery
        {
            After = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = id,
                SortField = "name",
                SortOrder = "desc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "desc"
        };

        // Act
        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, "dimension");
        var rendered = RenderFilter(filter);

        // Assert
        rendered.ShouldBe(new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("dimension", new BsonDocument("$lt", "environment")),
            new BsonDocument
            {
                { "dimension", "environment" },
                { "_id", new BsonDocument("$lt", new BsonBinaryData(id, GuidRepresentation.Standard)) }
            }
        }));
    }

    [Fact]
    public void BuildPageFilter_WithDescendingBeforeQuery_UsesGreaterThanTieBreaker()
    {
        // Arrange
        var id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6646");
        var query = new ListQuery
        {
            Before = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = id,
                SortField = "name",
                SortOrder = "desc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "desc"
        };

        // Act
        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, "dimension");
        var rendered = RenderFilter(filter);

        // Assert
        rendered.ShouldBe(new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("dimension", new BsonDocument("$gt", "environment")),
            new BsonDocument
            {
                { "dimension", "environment" },
                { "_id", new BsonDocument("$gt", new BsonBinaryData(id, GuidRepresentation.Standard)) }
            }
        }));
    }

    [Fact]
    public void BuildSort_WithAscendingBeforeQuery_ReversesSortOrder()
    {
        // Arrange
        var query = new ListQuery
        {
            Before = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6647"),
                SortField = "name",
                SortOrder = "asc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        // Act
        var sort = MongoCursorPagination.BuildSort<Scope>(query, "dimension");
        var rendered = RenderSort(sort);

        // Assert
        rendered.ShouldBe(new BsonDocument
        {
            { "dimension", -1 },
            { "_id", -1 }
        });
    }

    [Fact]
    public void BuildSort_WithDescendingBeforeQuery_ReversesSortOrder()
    {
        // Arrange
        var query = new ListQuery
        {
            Before = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6648"),
                SortField = "name",
                SortOrder = "desc",
                SortValue = "environment"
            }),
            SortField = "name",
            SortOrder = "desc"
        };

        // Act
        var sort = MongoCursorPagination.BuildSort<Scope>(query, "dimension");
        var rendered = RenderSort(sort);

        // Assert
        rendered.ShouldBe(new BsonDocument
        {
            { "dimension", 1 },
            { "_id", 1 }
        });
    }

    [Fact]
    public void DecodeForQuery_WithMismatchedSortSettings_ThrowsValidationException()
    {
        // Arrange
        var query = new ListQuery
        {
            After = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6649"),
                SortField = "createdAt",
                SortOrder = "desc",
                SortValue = new DateTimeOffset(2026, 03, 08, 12, 30, 45, TimeSpan.Zero)
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        // Act & Assert
        Should.Throw<ValidationException>(() => MongoCursorPagination.DecodeForQuery(query));
    }

    [Fact]
    public void MaterializePage_WithFirstPage_SetsNextCursorOnly()
    {
        // Arrange
        var query = new ListQuery
        {
            Limit = 2,
            SortField = "name",
            SortOrder = "asc"
        };

        var items = new[]
        {
            CreateScope("alpha", "01957d44-f548-7f0a-91f0-31c0e2df6650"),
            CreateScope("beta", "01957d44-f548-7f0a-91f0-31c0e2df6651"),
            CreateScope("gamma", "01957d44-f548-7f0a-91f0-31c0e2df6652")
        };

        // Act
        var result = MongoCursorPagination.MaterializePage(items, query, 3, scope => scope.Dimension, scope => scope.Id);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items[0].Dimension.ShouldBe("alpha");
        result.Items[1].Dimension.ShouldBe("beta");
        result.NextCursor.ShouldNotBeNull();
        result.PreviousCursor.ShouldBeNull();
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public void MaterializePage_WithAfterQuery_SetsPreviousCursor()
    {
        // Arrange
        var query = new ListQuery
        {
            Limit = 2,
            After = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6653"),
                SortField = "name",
                SortOrder = "asc",
                SortValue = "alpha"
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        var items = new[]
        {
            CreateScope("beta", "01957d44-f548-7f0a-91f0-31c0e2df6654"),
            CreateScope("gamma", "01957d44-f548-7f0a-91f0-31c0e2df6655")
        };

        // Act
        var result = MongoCursorPagination.MaterializePage(items, query, 4, scope => scope.Dimension, scope => scope.Id);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.NextCursor.ShouldBeNull();
        result.PreviousCursor.ShouldNotBeNull();
    }

    [Fact]
    public void MaterializePage_WithBeforeQuery_ReversesResultsBackToStableOrder()
    {
        // Arrange
        var query = new ListQuery
        {
            Limit = 2,
            Before = MongoCursorPagination.Encode(new PagingCursor
            {
                Id = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6656"),
                SortField = "name",
                SortOrder = "asc",
                SortValue = "delta"
            }),
            SortField = "name",
            SortOrder = "asc"
        };

        var items = new[]
        {
            CreateScope("charlie", "01957d44-f548-7f0a-91f0-31c0e2df6657"),
            CreateScope("bravo", "01957d44-f548-7f0a-91f0-31c0e2df6658"),
            CreateScope("alpha", "01957d44-f548-7f0a-91f0-31c0e2df6659")
        };

        // Act
        var result = MongoCursorPagination.MaterializePage(items, query, 4, scope => scope.Dimension, scope => scope.Id);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items[0].Dimension.ShouldBe("bravo");
        result.Items[1].Dimension.ShouldBe("charlie");
        result.NextCursor.ShouldNotBeNull();
        result.PreviousCursor.ShouldNotBeNull();
    }

    private static Scope CreateScope(string dimension, string id)
    {
        return new Scope
        {
            Id = Guid.Parse(id),
            Dimension = dimension,
            CreatedAt = new DateTimeOffset(2026, 03, 08, 12, 30, 45, TimeSpan.Zero),
            CreatedBy = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6660"),
            UpdatedAt = new DateTimeOffset(2026, 03, 08, 12, 30, 45, TimeSpan.Zero),
            UpdatedBy = Guid.Parse("01957d44-f548-7f0a-91f0-31c0e2df6661"),
            Version = 1
        };
    }

    private static BsonDocument RenderFilter<TDocument>(FilterDefinition<TDocument> filter)
    {
        return filter.Render(new RenderArgs<TDocument>(BsonSerializer.SerializerRegistry.GetSerializer<TDocument>(), BsonSerializer.SerializerRegistry));
    }

    private static BsonDocument RenderSort<TDocument>(SortDefinition<TDocument> sort)
    {
        return sort.Render(new RenderArgs<TDocument>(BsonSerializer.SerializerRegistry.GetSerializer<TDocument>(), BsonSerializer.SerializerRegistry));
    }
}