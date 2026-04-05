using System.Text.RegularExpressions;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ConfigEntryStore : IConfigEntryStore
{
    private static readonly SortFieldMap<ConfigEntry> SortFields = SortFieldMap<ConfigEntry>.Build("key", b => b
        .Field("key", "key", e => e.Key, collation: true)
        .Field("createdAt", "createdAt", e => e.CreatedAt)
        .Field("updatedAt", "updatedAt", e => e.UpdatedAt)
        .Field("version", "version", e => e.Version)
        .Field("id", "_id", e => e.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<ConfigEntry> _collection;

    public ConfigEntryStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _collection = context.GetCollection<ConfigEntry>(CollectionNames.ConfigEntries);
    }

    public async Task<ConfigEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(entry => entry.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<ConfigEntry>> ListAsync(ConfigEntryListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
    }

    public async Task CreateAsync(ConfigEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateKeyException(
                $"A config entry with key '{entry.Key}' already exists for this owner.",
                ex);
        }
    }

    public async Task<bool> UpdateAsync(ConfigEntry entry, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<ConfigEntry>.Filter.And(
            Builders<ConfigEntry>.Filter.Eq(entity => entity.Id, entry.Id),
            Builders<ConfigEntry>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<ConfigEntry>.Update
            .Set(entity => entity.ValueType, entry.ValueType)
            .Set(entity => entity.Values, entry.Values)
            .Set(entity => entity.IsSensitive, entry.IsSensitive)
            .Set(entity => entity.Description, entry.Description)
            .Set(entity => entity.UpdatedAt, entry.UpdatedAt)
            .Set(entity => entity.UpdatedBy, entry.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        entry.Version = nextVersion;
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _collection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

    public async Task<IReadOnlyList<ConfigEntry>> GetAllByOwnerAsync(Guid ownerId, ConfigEntryOwnerType ownerType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ConfigEntry>.Filter.And(
            Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerId, ownerId),
            Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerType, ownerType));

        return await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAllByOwnerAsync(Guid ownerId, ConfigEntryOwnerType ownerType, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ConfigEntry>.Filter.And(
            Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerId, ownerId),
            Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerType, ownerType));

        await _collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    private static FilterDefinition<ConfigEntry> BuildEntityFilter(ConfigEntryListQuery query)
    {
        var filters = new List<FilterDefinition<ConfigEntry>>();

        if (query.OwnerId.HasValue)
        {
            filters.Add(Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerId, query.OwnerId.Value));
        }

        if (query.OwnerType.HasValue)
        {
            filters.Add(Builders<ConfigEntry>.Filter.Eq(entry => entry.OwnerType, query.OwnerType.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.KeyPrefix))
        {
            var escapedPrefix = Regex.Escape(query.KeyPrefix);
            filters.Add(Builders<ConfigEntry>.Filter.Regex(entry => entry.Key, new BsonRegularExpression($"^{escapedPrefix}", "i")));
        }

        return filters.Count == 0
            ? FilterDefinition<ConfigEntry>.Empty
            : Builders<ConfigEntry>.Filter.And(filters);
    }
}