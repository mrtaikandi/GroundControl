using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Infrastructure;

/// <summary>
/// Defines the "MongoDB" xUnit collection so all tests sharing this fixture reuse one container.
/// </summary>
[CollectionDefinition("MongoDB")]
public sealed class MongoCollectionFixture : ICollectionFixture<MongoFixture>;