using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Default MongoDB context implementation for GroundControl.
/// </summary>
public sealed class MongoDbContext : IMongoDbContext
{
    private readonly string? _collectionPrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbContext" /> class.
    /// </summary>
    /// <param name="client">The MongoDB client.</param>
    /// <param name="options">The MongoDB options.</param>
    public MongoDbContext(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        Database = client.GetDatabase(options.Value.DatabaseName);
        _collectionPrefix = options.Value.CollectionPrefix;
    }

    /// <inheritdoc />
    public string ConnectionString => Database.Client.Settings.Server.ToString();

    /// <inheritdoc />
    public IMongoDatabase Database { get; }

    /// <inheritdoc />
    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        collectionName = string.IsNullOrEmpty(_collectionPrefix) ? collectionName : $"{_collectionPrefix}{collectionName}";
        return Database.GetCollection<T>(collectionName);
    }

    /// <inheritdoc />
    public Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default) =>
        Database.Client.StartSessionAsync(new ClientSessionOptions { CausalConsistency = true }, cancellationToken);
}