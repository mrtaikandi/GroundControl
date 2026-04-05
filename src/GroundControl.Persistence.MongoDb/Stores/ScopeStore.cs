using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ScopeStore : IScopeStore
{
    private static readonly SortFieldMap<Scope> SortFields = SortFieldMap<Scope>.Build("dimension", b => b
        .Field("dimension", "dimension", s => s.Dimension, collation: true)
        .Alias("name", "dimension")
        .Field("createdAt", "createdAt", s => s.CreatedAt)
        .Field("updatedAt", "updatedAt", s => s.UpdatedAt)
        .Field("version", "version", s => s.Version)
        .Field("id", "_id", s => s.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Client> _clientCollection;
    private readonly IMongoCollection<ConfigEntry> _configEntryCollection;
    private readonly IMongoCollection<Scope> _scopeCollection;
    private readonly IMongoCollection<Snapshot> _snapshotCollection;
    private readonly IMongoCollection<Variable> _variableCollection;

    public ScopeStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _scopeCollection = context.GetCollection<Scope>(CollectionNames.Scopes);
        _configEntryCollection = context.GetCollection<ConfigEntry>(CollectionNames.ConfigEntries);
        _variableCollection = context.GetCollection<Variable>(CollectionNames.Variables);
        _clientCollection = context.GetCollection<Client>(CollectionNames.Clients);
        _snapshotCollection = context.GetCollection<Snapshot>(CollectionNames.Snapshots);
    }

    public async Task<Scope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _scopeCollection.Find(scope => scope.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Scope?> GetByDimensionAsync(string dimension, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);

        var filter = Builders<Scope>.Filter.Eq(scope => scope.Dimension, dimension);
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _scopeCollection.Find(filter, options).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<Scope>> ListAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _scopeCollection.ExecutePagedQueryAsync(query, SortFields, _context, cancellationToken);
    }

    public Task CreateAsync(Scope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return _scopeCollection.InsertOneAsync(scope, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsync(Scope scope, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Scope>.Filter.And(
            Builders<Scope>.Filter.Eq(entity => entity.Id, scope.Id),
            Builders<Scope>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<Scope>.Update
            .Set(entity => entity.Dimension, scope.Dimension)
            .Set(entity => entity.AllowedValues, scope.AllowedValues)
            .Set(entity => entity.Description, scope.Description)
            .Set(entity => entity.UpdatedAt, scope.UpdatedAt)
            .Set(entity => entity.UpdatedBy, scope.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _scopeCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        scope.Version = nextVersion;
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _scopeCollection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

    public async Task<bool> IsReferencedAsync(string dimension, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentNullException.ThrowIfNull(value);

        if (await AnyMatchesAsync(_configEntryCollection, $"values.scopes.{dimension}", value, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await AnyMatchesAsync(_variableCollection, $"values.scopes.{dimension}", value, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await AnyMatchesAsync(_clientCollection, $"scopes.{dimension}", value, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return await AnyMatchesAsync(_snapshotCollection, $"entries.values.scopes.{dimension}", value, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> AnyMatchesAsync<TDocument>(IMongoCollection<TDocument> collection, string fieldPath, string value, CancellationToken cancellationToken)
        where TDocument : class
    {
        var filter = Builders<TDocument>.Filter.Eq(fieldPath, value);
        var count = await collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        return count > 0;
    }
}