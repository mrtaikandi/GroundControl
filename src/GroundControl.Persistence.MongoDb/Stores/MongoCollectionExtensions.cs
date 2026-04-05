using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

/// <summary>
/// Extension methods for common MongoDB collection operations.
/// </summary>
internal static class MongoCollectionExtensions
{
    /// <summary>
    /// Deletes a document by ID with optimistic concurrency control.
    /// Uses BSON field names <c>_id</c> and <c>version</c> which are guaranteed
    /// by the project's <see cref="Conventions.MongoConventions"/> registration.
    /// </summary>
    public static async Task<bool> DeleteWithVersionAsync<TDocument>(
        this IMongoCollection<TDocument> collection,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TDocument>.Filter.And(
            Builders<TDocument>.Filter.Eq("_id", id),
            Builders<TDocument>.Filter.Eq("version", expectedVersion));

        var result = await collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }
}