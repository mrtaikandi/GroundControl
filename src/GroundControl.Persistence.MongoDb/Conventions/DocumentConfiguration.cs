using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

/// <summary>
/// Provides a base class for typed MongoDB document configurations.
/// </summary>
/// <typeparam name="TCollection">The collection document type.</typeparam>
public abstract class DocumentConfiguration<TCollection> : IDocumentConfiguration<TCollection>
    where TCollection : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentConfiguration{TCollection}"/> class.
    /// </summary>
    /// <param name="context">The MongoDB context.</param>
    /// <param name="collectionName">The explicit collection name.</param>
    protected DocumentConfiguration(IMongoDbContext context, string collectionName)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        Collection = Context.GetCollection<TCollection>(collectionName);
    }

    /// <inheritdoc />
    public IMongoCollection<TCollection> Collection { get; }

    /// <inheritdoc />
    public IMongoDbContext Context { get; }

    /// <inheritdoc />
    public abstract Task ConfigureAsync(CancellationToken cancellationToken = default);
}