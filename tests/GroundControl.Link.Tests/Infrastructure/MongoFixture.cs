using System.Collections.Concurrent;
using GroundControl.Persistence.MongoDb.Conventions;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace GroundControl.Link.Tests.Infrastructure;

/// <summary>
/// Shared test fixture that starts a MongoDB replica set container via Testcontainers.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8")
        .WithReplicaSet()
        .Build();

    private readonly ConcurrentBag<MongoClient> _clients = [];

    /// <summary>
    /// Gets the MongoDB connection string for the running container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Creates a new <see cref="IMongoDatabase"/> with a unique name for test isolation.
    /// </summary>
    public IMongoDatabase CreateDatabase()
    {
        var client = new MongoClient(ConnectionString);
        _clients.Add(client);

        var databaseName = $"groundcontrol_test_{Guid.CreateVersion7():N}";
        return client.GetDatabase(databaseName);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        MongoConventions.Register();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }
}