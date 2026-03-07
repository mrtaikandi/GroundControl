using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Provides access to the configured MongoDB database and collections.
/// </summary>
public interface IMongoDbContext
{
    /// <summary>
    /// Gets the MongoDB connection string used to connect to the database.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the configured MongoDB database.
    /// </summary>
    IMongoDatabase Database { get; }

    /// <summary>
    /// Gets a MongoDB collection using the explicit collection name.
    /// </summary>
    /// <typeparam name="TDocument">The document type stored in the collection.</typeparam>
    /// <param name="collectionName">The explicit collection name.</param>
    /// <returns>The MongoDB collection.</returns>
    IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName);

    /// <summary>
    /// Creates a client session for transaction support.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A MongoDB client session.</returns>
    Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default);
}