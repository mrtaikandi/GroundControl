using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.MongoDb.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Scopes;

[Collection("MongoDB")]
public sealed class ScopeStoreTests
{
    private readonly MongoFixture _mongoFixture;

    public ScopeStoreTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task ConfigureAsync_WithScopesCollection_CreatesCaseInsensitiveUniqueDimensionIndex()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = _mongoFixture.CreateDatabase();
        var context = CreateContext(database);
        var configuration = new ScopeConfiguration(context);

        // Act
        await configuration.ConfigureAsync(cancellationToken);
        using var cursor = await database.GetCollection<BsonDocument>("scopes").Indexes.ListAsync(cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);

        // Assert
        var dimensionIndex = indexes.Single(index => index["name"] == "ux_scopes_dimension");
        dimensionIndex["unique"].AsBoolean.ShouldBeTrue();
        dimensionIndex["key"].AsBsonDocument["dimension"].AsInt32.ShouldBe(1);
        dimensionIndex["collation"]["locale"].AsString.ShouldBe("en");
        dimensionIndex["collation"]["strength"].AsInt32.ShouldBe(2);
    }

    [Fact]
    public async Task CreateGetUpdateDeleteAsync_WithMatchingVersions_PersistsScopeChanges()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var scope = CreateScope("environment", "dev", "prod");

        // Act
        await store.CreateAsync(scope, cancellationToken);
        var byId = await store.GetByIdAsync(scope.Id, cancellationToken);
        var byDimension = await store.GetByDimensionAsync("ENVIRONMENT", cancellationToken);

        scope.Description = "Deployment environment";
        scope.AllowedValues.Clear();
        scope.AllowedValues.Add("stage");
        scope.AllowedValues.Add("prod");
        scope.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await store.UpdateAsync(scope, expectedVersion: 1, cancellationToken);
        var reloaded = await store.GetByIdAsync(scope.Id, cancellationToken);
        var deleted = await store.DeleteAsync(scope.Id, expectedVersion: 2, cancellationToken);
        var missing = await store.GetByIdAsync(scope.Id, cancellationToken);

        // Assert
        byId.ShouldNotBeNull();
        byId.Dimension.ShouldBe("environment");
        byDimension.ShouldNotBeNull();
        byDimension.Id.ShouldBe(scope.Id);
        updated.ShouldBeTrue();
        reloaded.ShouldNotBeNull();
        reloaded.Version.ShouldBe(2);
        reloaded.Description.ShouldBe("Deployment environment");
        reloaded.AllowedValues.ShouldBe(["stage", "prod"]);
        deleted.ShouldBeTrue();
        missing.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithStaleVersion_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var scope = CreateScope("region", "eu", "us");
        await store.CreateAsync(scope, cancellationToken);

        scope.Description = "Updated description";
        scope.UpdatedAt = DateTimeOffset.UtcNow;

        // Act
        var updated = await store.UpdateAsync(scope, expectedVersion: 2, cancellationToken);

        // Assert
        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_WithForwardAndBackwardPagination_ReturnsExpectedPages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateScope("gamma", "g1"), cancellationToken);
        await store.CreateAsync(CreateScope("alpha", "a1"), cancellationToken);
        await store.CreateAsync(CreateScope("beta", "b1"), cancellationToken);

        // Act
        var firstPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            SortField = "dimension",
            SortOrder = "asc"
        }, cancellationToken);

        var secondPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            After = firstPage.NextCursor,
            SortField = "dimension",
            SortOrder = "asc"
        }, cancellationToken);

        var previousPage = await store.ListAsync(new ListQuery
        {
            Limit = 2,
            Before = secondPage.PreviousCursor,
            SortField = "dimension",
            SortOrder = "asc"
        }, cancellationToken);

        // Assert
        firstPage.Items.Select(scope => scope.Dimension).ShouldBe(["alpha", "beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondPage.Items.Select(scope => scope.Dimension).ShouldBe(["gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousPage.Items.Select(scope => scope.Dimension).ShouldBe(["alpha", "beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task IsReferencedAsync_WhenClientUsesDimensionValue_ReturnsTrue()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, database) = await CreateStoreAsync(cancellationToken);
        var clientCollection = database.GetCollection<Client>("clients");
        var timestamp = DateTimeOffset.UtcNow;

        await clientCollection.InsertOneAsync(new Client
        {
            Id = Guid.CreateVersion7(),
            ProjectId = Guid.CreateVersion7(),
            Scopes = new Dictionary<string, string>
            {
                ["environment"] = "prod"
            },
            Secret = "secret",
            Name = "test-client",
            IsActive = true,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: cancellationToken);

        // Act
        var referenced = await store.IsReferencedAsync("environment", "prod", cancellationToken);
        var missing = await store.IsReferencedAsync("environment", "dev", cancellationToken);

        // Assert
        referenced.ShouldBeTrue();
        missing.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateDimensionDifferentCasing_ThrowsMongoWriteException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateScope("environment", "dev", "prod"), cancellationToken);

        // Act & Assert
        await Should.ThrowAsync<MongoWriteException>(() => store.CreateAsync(CreateScope("Environment", "qa"), cancellationToken));
    }

    private async Task<(ScopeStore Store, IMongoDatabase Database)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var database = _mongoFixture.CreateDatabase();
        var context = CreateContext(database);
        var configuration = new ScopeConfiguration(context);

        await configuration.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        return (new ScopeStore(context), database);
    }

    private MongoDbContext CreateContext(IMongoDatabase database)
    {
        var options = Options.Create(new MongoDbOptions
        {
            ConnectionString = _mongoFixture.ConnectionString,
            DatabaseName = database.DatabaseNamespace.DatabaseName
        });

        return new MongoDbContext(database.Client, options);
    }

    private static Scope CreateScope(string dimension, params string[] allowedValues)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return new Scope
        {
            Id = Guid.CreateVersion7(),
            Dimension = dimension,
            AllowedValues = [.. allowedValues],
            Description = $"{dimension} scope",
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        };
    }
}