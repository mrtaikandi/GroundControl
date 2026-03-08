using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Provides access to the configured MongoDB database and collections.
/// </summary>
public interface IMongoDbContext
{
    /// <summary>
    /// The default case-insensitive collation used for all collections,
    /// which is based on the English locale and has a strength of "Secondary" to ignore case differences.
    /// </summary>
    private static readonly Collation CaseInsensitiveCollation = new("en", strength: CollationStrength.Secondary);

    /// <summary>
    /// Gets the MongoDB connection string used to connect to the database.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the configured MongoDB database.
    /// </summary>
    IMongoDatabase Database { get; }

    /// <summary>
    /// Gets the default collation for all collections, which is case-insensitive by default.
    /// </summary>
    Collation DefaultCollation => CaseInsensitiveCollation;

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