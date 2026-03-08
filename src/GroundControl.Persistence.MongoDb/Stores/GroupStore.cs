using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class GroupStore : IGroupStore
{
    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Group> _groupCollection;
    private readonly IMongoCollection<Project> _projectCollection;
    private readonly IMongoCollection<Template> _templateCollection;
    private readonly IMongoCollection<Variable> _variableCollection;

    public GroupStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _groupCollection = context.GetCollection<Group>(CollectionNames.Groups);
        _projectCollection = context.GetCollection<Project>(CollectionNames.Projects);
        _templateCollection = context.GetCollection<Template>(CollectionNames.Templates);
        _variableCollection = context.GetCollection<Variable>(CollectionNames.Variables);
    }

    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _groupCollection.Find(group => group.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var filter = Builders<Group>.Filter.Eq(group => group.Name, name);
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _groupCollection.Find(filter, options).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<Group>> ListAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var filter = MongoCursorPagination.BuildPageFilter<Group>(query, bsonSortField);
        var sort = MongoCursorPagination.BuildSort<Group>(query, bsonSortField);
        var findOptions = new FindOptions
        {
            Collation = GetCollation(sortField)
        };

        var items = await _groupCollection
            .Find(filter, findOptions)
            .Sort(sort)
            .Limit(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _groupCollection
            .CountDocumentsAsync(FilterDefinition<Group>.Empty, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MongoCursorPagination.MaterializePage(
            items,
            query,
            totalCount,
            group => GetSortValue(group, sortField),
            group => group.Id);
    }

    public Task CreateAsync(Group group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        return _groupCollection.InsertOneAsync(group, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsync(Group group, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Group>.Filter.And(
            Builders<Group>.Filter.Eq(entity => entity.Id, group.Id),
            Builders<Group>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<Group>.Update
            .Set(entity => entity.Name, group.Name)
            .Set(entity => entity.Description, group.Description)
            .Set(entity => entity.UpdatedAt, group.UpdatedAt)
            .Set(entity => entity.UpdatedBy, group.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _groupCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        group.Version = nextVersion;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Group>.Filter.And(
            Builders<Group>.Filter.Eq(entity => entity.Id, id),
            Builders<Group>.Filter.Eq(entity => entity.Version, expectedVersion));

        var result = await _groupCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

    public async Task<bool> HasDependentsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (await AnyWithGroupIdAsync(_projectCollection, groupId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await AnyWithGroupIdAsync(_templateCollection, groupId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return await AnyWithGroupIdAsync(_variableCollection, groupId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> AnyWithGroupIdAsync<TDocument>(IMongoCollection<TDocument> collection, Guid groupId, CancellationToken cancellationToken)
        where TDocument : class
    {
        var filter = Builders<TDocument>.Filter.Eq("groupId", groupId);
        var count = await collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        return count > 0;
    }

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "name", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(Group group, string sortField) => sortField switch
    {
        "name" => group.Name,
        "createdAt" => group.CreatedAt,
        "updatedAt" => group.UpdatedAt,
        "version" => group.Version,
        "id" => group.Id,
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string GetBsonSortField(string sortField) => sortField switch
    {
        "name" => "name",
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
            return "name";
        }

        return sortField.Trim() switch
        {
            var value when value.Equals("name", StringComparison.OrdinalIgnoreCase) => "name",
            var value when value.Equals("createdAt", StringComparison.OrdinalIgnoreCase) => "createdAt",
            var value when value.Equals("updatedAt", StringComparison.OrdinalIgnoreCase) => "updatedAt",
            var value when value.Equals("version", StringComparison.OrdinalIgnoreCase) => "version",
            var value when value.Equals("id", StringComparison.OrdinalIgnoreCase) => "id",
            _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
        };
    }
}