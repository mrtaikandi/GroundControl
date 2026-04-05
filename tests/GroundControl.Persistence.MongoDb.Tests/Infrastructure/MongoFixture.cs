using System.Collections.Concurrent;
using GroundControl.Persistence.MongoDb.Conventions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Infrastructure;

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

    /// <summary>
    /// Creates a <see cref="MongoDbContext"/> for the given database.
    /// </summary>
    public MongoDbContext CreateContext(IMongoDatabase database)
    {
        var options = Options.Create(new MongoDbOptions
        {
            ConnectionString = ConnectionString,
            DatabaseName = database.DatabaseNamespace.DatabaseName
        });

        return new MongoDbContext(database.Client, options);
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