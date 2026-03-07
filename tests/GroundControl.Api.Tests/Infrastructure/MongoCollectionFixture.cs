using Xunit;

namespace GroundControl.Api.Tests.Infrastructure;

/// <summary>
/// Defines the "MongoDB" xUnit collection so all tests sharing this fixture reuse one container.
/// </summary>
[CollectionDefinition("MongoDB")]
public sealed class MongoCollectionFixture : ICollectionFixture<MongoFixture>;