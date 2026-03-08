using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ScopeStore : IScopeStore
{
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

    public async Task<PagedResult<Scope>> ListAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var filter = MongoCursorPagination.BuildPageFilter<Scope>(query, bsonSortField);
        var sort = MongoCursorPagination.BuildSort<Scope>(query, bsonSortField);
        var findOptions = new FindOptions
        {
            Collation = GetCollation(sortField)
        };

        var items = await _scopeCollection
            .Find(filter, findOptions)
            .Sort(sort)
            .Limit(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _scopeCollection
            .CountDocumentsAsync(FilterDefinition<Scope>.Empty, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MongoCursorPagination.MaterializePage(
            items,
            query,
            totalCount,
            scope => GetSortValue(scope, sortField),
            scope => scope.Id);
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

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Scope>.Filter.And(
            Builders<Scope>.Filter.Eq(entity => entity.Id, id),
            Builders<Scope>.Filter.Eq(entity => entity.Version, expectedVersion));

        var result = await _scopeCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

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

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "dimension", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(Scope scope, string sortField) => sortField switch
    {
        "dimension" => scope.Dimension,
        "createdAt" => scope.CreatedAt,
        "updatedAt" => scope.UpdatedAt,
        "version" => scope.Version,
        "id" => scope.Id,
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string GetBsonSortField(string sortField) => sortField switch
    {
        "dimension" => "dimension",
        "createdAt" => "createdAt",
        "updatedAt" => "updatedAt",
        "version" => "version",
        "id" => "_id",
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string NormalizeSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return "dimension";
        }

        return sortField.Trim() switch
        {
            var value when value.Equals("name", StringComparison.OrdinalIgnoreCase) => "dimension",
            var value when value.Equals("dimension", StringComparison.OrdinalIgnoreCase) => "dimension",
            var value when value.Equals("createdAt", StringComparison.OrdinalIgnoreCase) => "createdAt",
            var value when value.Equals("updatedAt", StringComparison.OrdinalIgnoreCase) => "updatedAt",
            var value when value.Equals("version", StringComparison.OrdinalIgnoreCase) => "version",
            var value when value.Equals("id", StringComparison.OrdinalIgnoreCase) => "id",
            _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
        };
    }
}