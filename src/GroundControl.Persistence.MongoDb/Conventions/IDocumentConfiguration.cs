using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

/// <summary>
/// Defines a startup configuration step for a MongoDB document collection.
/// </summary>
public interface IDocumentConfiguration
{
    /// <summary>
    /// Gets the MongoDB context used by the configuration.
    /// </summary>
    IMongoDbContext Context { get; }

    /// <summary>
    /// Configures the collection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when configuration finishes.</returns>
    Task ConfigureAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a startup configuration step for a typed MongoDB collection.
/// </summary>
/// <typeparam name="TCollection">The collection document type.</typeparam>
public interface IDocumentConfiguration<TCollection> : IDocumentConfiguration
    where TCollection : class
{
    /// <summary>
    /// Gets the MongoDB collection instance.
    /// </summary>
    IMongoCollection<TCollection> Collection { get; }
}