using GroundControl.Persistence.MongoDb.Conventions;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace GroundControl.Api.Tests.Infrastructure;

/// <summary>
/// Shared test fixture that starts a MongoDB replica set container via Testcontainers.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8")
        .WithReplicaSet()
        .WithReuse(true)
        .Build();

    private MongoClient? _client;

    /// <summary>
    /// Gets the MongoDB connection string for the running container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Creates a new <see cref="IMongoDatabase"/> with a unique name for test isolation.
    /// </summary>
    public IMongoDatabase CreateDatabase()
    {
        var databaseName = $"groundcontrol_test_{Guid.CreateVersion7():N}";
        return _client!.GetDatabase(databaseName);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _client = new MongoClient(ConnectionString);
        MongoConventions.Register();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}