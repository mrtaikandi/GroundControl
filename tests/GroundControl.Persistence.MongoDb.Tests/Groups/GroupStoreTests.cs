using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.MongoDb.Tests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Groups;

[Collection("MongoDB")]
public sealed class GroupStoreTests
{
    private readonly MongoFixture _mongoFixture;

    public GroupStoreTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task ConfigureAsync_WithGroupsCollection_CreatesCaseInsensitiveUniqueNameIndex()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new GroupConfiguration(context);

        // Act
        await configuration.ConfigureAsync(cancellationToken);
        using var cursor = await database.GetCollection<BsonDocument>("groups").Indexes.ListAsync(cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);

        // Assert
        var nameIndex = indexes.Single(index => index["name"] == "ux_groups_name");
        nameIndex["unique"].AsBoolean.ShouldBeTrue();
        nameIndex["key"].AsBsonDocument["name"].AsInt32.ShouldBe(1);
        nameIndex["collation"]["locale"].AsString.ShouldBe("en");
        nameIndex["collation"]["strength"].AsInt32.ShouldBe(2);
    }

    [Fact]
    public async Task CreateGetUpdateDeleteAsync_WithMatchingVersions_PersistsGroupChanges()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var group = CreateGroup("Engineering");

        // Act
        await store.CreateAsync(group, cancellationToken);
        var byId = await store.GetByIdAsync(group.Id, cancellationToken);
        var byName = await store.GetByNameAsync("ENGINEERING", cancellationToken);

        group.Description = "Engineering team";
        group.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await store.UpdateAsync(group, expectedVersion: 1, cancellationToken);
        var reloaded = await store.GetByIdAsync(group.Id, cancellationToken);
        var deleted = await store.DeleteAsync(group.Id, expectedVersion: 2, cancellationToken);
        var missing = await store.GetByIdAsync(group.Id, cancellationToken);

        // Assert
        byId.ShouldNotBeNull();
        byId.Name.ShouldBe("Engineering");
        byName.ShouldNotBeNull();
        byName.Id.ShouldBe(group.Id);
        updated.ShouldBeTrue();
        reloaded.ShouldNotBeNull();
        reloaded.Version.ShouldBe(2);
        reloaded.Description.ShouldBe("Engineering team");
        deleted.ShouldBeTrue();
        missing.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithStaleVersion_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var group = CreateGroup("Platform");
        await store.CreateAsync(group, cancellationToken);

        group.Description = "Updated description";
        group.UpdatedAt = DateTimeOffset.UtcNow;

        // Act
        var updated = await store.UpdateAsync(group, expectedVersion: 2, cancellationToken);

        // Assert
        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_WithForwardAndBackwardPagination_ReturnsExpectedPages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateGroup("Gamma"), cancellationToken);
        await store.CreateAsync(CreateGroup("Alpha"), cancellationToken);
        await store.CreateAsync(CreateGroup("Beta"), cancellationToken);

        // Act
        var firstPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        var secondPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            After = firstPage.NextCursor,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        var previousPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            Before = secondPage.PreviousCursor,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        // Assert
        firstPage.Items.Select(group => group.Name).ShouldBe(["Alpha", "Beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondPage.Items.Select(group => group.Name).ShouldBe(["Gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousPage.Items.Select(group => group.Name).ShouldBe(["Alpha", "Beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenProjectReferencesGroup_ReturnsTrue()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, database) = await CreateStoreAsync(cancellationToken);
        var group = CreateGroup("Engineering");
        await store.CreateAsync(group, cancellationToken);

        var projectCollection = database.GetCollection<Project>("projects");
        var timestamp = DateTimeOffset.UtcNow;

        await projectCollection.InsertOneAsync(new Project
        {
            Id = Guid.CreateVersion7(),
            Name = "test-project",
            GroupId = group.Id,
            TemplateIds = [],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: cancellationToken);

        // Act
        var hasDependents = await store.HasDependentsAsync(group.Id, cancellationToken);

        // Assert
        hasDependents.ShouldBeTrue();
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoDependents_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var group = CreateGroup("Isolated");
        await store.CreateAsync(group, cancellationToken);

        // Act
        var hasDependents = await store.HasDependentsAsync(group.Id, cancellationToken);

        // Assert
        hasDependents.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNameDifferentCasing_ThrowsMongoWriteException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateGroup("Engineering"), cancellationToken);

        // Act & Assert
        await Should.ThrowAsync<MongoWriteException>(() => store.CreateAsync(CreateGroup("engineering"), cancellationToken));
    }

    private async Task<(GroupStore Store, IMongoDatabase Database)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new GroupConfiguration(context);

        await configuration.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        return (new GroupStore(context), database);
    }

    private static Group CreateGroup(string name)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return new Group
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = $"{name} group",
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        };
    }
}