using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.MongoDb.Pagination;
using MongoDB.Driver;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Pagination;

public sealed class SortFieldMapTests
{
    private static readonly SortFieldMap<TestEntity> Map = SortFieldMap<TestEntity>.Build("name", b => b
        .Field("name", "name", e => e.Name, collation: true)
        .Field("createdAt", "createdAt", e => e.CreatedAt)
        .Field("id", "_id", e => e.Id));

    [Fact]
    public void Normalize_WithNullInput_ReturnsDefaultField()
    {
        // Arrange & Act
        var result = Map.Normalize(null);

        // Assert
        result.ShouldBe("name");
    }

    [Fact]
    public void Normalize_WithEmptyInput_ReturnsDefaultField()
    {
        // Arrange & Act
        var result = Map.Normalize("   ");

        // Assert
        result.ShouldBe("name");
    }

    [Theory]
    [InlineData("name", "name")]
    [InlineData("NAME", "name")]
    [InlineData("Name", "name")]
    [InlineData("createdAt", "createdAt")]
    [InlineData("CREATEDAT", "createdAt")]
    [InlineData("id", "id")]
    public void Normalize_WithValidInput_ReturnsCaseCorrectedField(string input, string expected)
    {
        // Arrange & Act
        var result = Map.Normalize(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Normalize_WithWhitespacePadding_TrimsAndResolves()
    {
        // Arrange & Act
        var result = Map.Normalize("  name  ");

        // Assert
        result.ShouldBe("name");
    }

    [Fact]
    public void Normalize_WithUnsupportedField_ThrowsValidationException()
    {
        // Arrange & Act
        var exception = Record.Exception(() => Map.Normalize("unknown"));

        // Assert
        exception.ShouldBeOfType<ValidationException>();
        exception.Message.ShouldContain("unknown");
    }

    [Theory]
    [InlineData("name", "name")]
    [InlineData("createdAt", "createdAt")]
    [InlineData("id", "_id")]
    public void GetBsonField_WithNormalizedField_ReturnsBsonName(string field, string expectedBson)
    {
        // Arrange & Act
        var result = Map.GetBsonField(field);

        // Assert
        result.ShouldBe(expectedBson);
    }

    [Fact]
    public void GetBsonField_WithUnknownField_ThrowsValidationException()
    {
        // Arrange & Act
        var exception = Record.Exception(() => Map.GetBsonField("unknown"));

        // Assert
        exception.ShouldBeOfType<ValidationException>();
    }

    [Fact]
    public void GetSortValue_WithNameField_ReturnsEntityName()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.CreateVersion7(), Name = "TestValue", CreatedAt = DateTimeOffset.UtcNow };

        // Act
        var result = Map.GetSortValue(entity, "name");

        // Assert
        result.ShouldBe("TestValue");
    }

    [Fact]
    public void GetSortValue_WithIdField_ReturnsEntityId()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var entity = new TestEntity { Id = id, Name = "Test", CreatedAt = DateTimeOffset.UtcNow };

        // Act
        var result = Map.GetSortValue(entity, "id");

        // Assert
        result.ShouldBe(id);
    }

    [Fact]
    public void GetCollation_WithCollationField_ReturnsDefaultCollation()
    {
        // Arrange
        var expectedCollation = new Collation("en", strength: CollationStrength.Secondary);
        var context = Substitute.For<IMongoDbContext>();
        context.DefaultCollation.Returns(expectedCollation);

        // Act
        var result = Map.GetCollation("name", context);

        // Assert
        result.ShouldBe(expectedCollation);
    }

    [Fact]
    public void GetCollation_WithNonCollationField_ReturnsNull()
    {
        // Arrange
        var context = Substitute.For<IMongoDbContext>();

        // Act
        var result = Map.GetCollation("createdAt", context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Alias_WithAliasInput_ResolvesToTargetField()
    {
        // Arrange
        var map = SortFieldMap<TestEntity>.Build("dimension", b => b
            .Field("dimension", "dimension", e => e.Name, collation: true)
            .Alias("name", "dimension")
            .Field("id", "_id", e => e.Id));

        // Act
        var result = map.Normalize("name");

        // Assert
        result.ShouldBe("dimension");
    }

    [Fact]
    public void Alias_WithCaseInsensitiveInput_ResolvesToTargetField()
    {
        // Arrange
        var map = SortFieldMap<TestEntity>.Build("dimension", b => b
            .Field("dimension", "dimension", e => e.Name, collation: true)
            .Alias("name", "dimension")
            .Field("id", "_id", e => e.Id));

        // Act
        var result = map.Normalize("NAME");

        // Assert
        result.ShouldBe("dimension");
    }

    [Fact]
    public void Build_WithDefaultFieldNotRegistered_ThrowsArgumentException()
    {
        // Arrange & Act
        var exception = Record.Exception(() => SortFieldMap<TestEntity>.Build("missing", b => b
            .Field("name", "name", e => e.Name)));

        // Assert
        exception.ShouldBeOfType<ArgumentException>();
        exception.Message.ShouldContain("missing");
    }

    [Fact]
    public void Build_WithDefaultFieldAsAlias_Succeeds()
    {
        // Arrange & Act
        var map = SortFieldMap<TestEntity>.Build("name", b => b
            .Field("dimension", "dimension", e => e.Name)
            .Alias("name", "dimension")
            .Field("id", "_id", e => e.Id));

        // Assert
        var result = map.Normalize(null);
        result.ShouldBe("name");
    }

    internal sealed class TestEntity
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}