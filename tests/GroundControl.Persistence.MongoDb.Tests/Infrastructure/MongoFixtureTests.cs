using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Infrastructure;

[Collection("MongoDB")]
public sealed class MongoFixtureTests
{
    private readonly MongoFixture _fixture;

    public MongoFixtureTests(MongoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateDatabase_CanInsertAndRetrieveDocument()
    {
        // Arrange
        var database = _fixture.CreateDatabase();
        var collection = database.GetCollection<BsonDocument>("test");
        var document = new BsonDocument("key", "value");

        // Act
        await collection.InsertOneAsync(document, cancellationToken: TestContext.Current.CancellationToken);
        var result = await collection.Find(new BsonDocument("key", "value"))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result["key"].AsString.ShouldBe("value");
    }

    [Fact]
    public void CreateDatabase_ReturnsDatabaseWithUniqueName()
    {
        // Arrange & Act
        var database1 = _fixture.CreateDatabase();
        var database2 = _fixture.CreateDatabase();

        // Assert
        database1.DatabaseNamespace.DatabaseName.ShouldNotBe(database2.DatabaseNamespace.DatabaseName);
        database1.DatabaseNamespace.DatabaseName.ShouldStartWith("groundcontrol_test_");
        database2.DatabaseNamespace.DatabaseName.ShouldStartWith("groundcontrol_test_");
    }
}