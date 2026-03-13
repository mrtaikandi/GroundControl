using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class VariableStore : IVariableStore
{
    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Variable> _collection;

    public VariableStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _collection = context.GetCollection<Variable>(CollectionNames.Variables);
    }

    public async Task<Variable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(v => v.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<Variable>> ListAsync(VariableListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var pageFilter = MongoCursorPagination.BuildPageFilter<Variable>(query, bsonSortField);
        var entityFilter = BuildEntityFilter(query);
        var combinedFilter = Builders<Variable>.Filter.And(entityFilter, pageFilter);

        var sort = MongoCursorPagination.BuildSort<Variable>(query, bsonSortField);
        var findOptions = new FindOptions
        {
            Collation = GetCollation(sortField)
        };

        var items = await _collection
            .Find(combinedFilter, findOptions)
            .Sort(sort)
            .Limit(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _collection
            .CountDocumentsAsync(entityFilter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MongoCursorPagination.MaterializePage(
            items,
            query,
            totalCount,
            v => GetSortValue(v, sortField),
            v => v.Id);
    }

    public async Task CreateAsync(Variable variable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variable);

        try
        {
            await _collection.InsertOneAsync(variable, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateKeyException(
                $"A variable with name '{variable.Name}' already exists for this owner.",
                ex);
        }
    }

    public async Task<bool> UpdateAsync(Variable variable, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variable);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Variable>.Filter.And(
            Builders<Variable>.Filter.Eq(v => v.Id, variable.Id),
            Builders<Variable>.Filter.Eq(v => v.Version, expectedVersion));

        var update = Builders<Variable>.Update
            .Set(v => v.Description, variable.Description)
            .Set(v => v.Values, variable.Values)
            .Set(v => v.IsSensitive, variable.IsSensitive)
            .Set(v => v.UpdatedAt, variable.UpdatedAt)
            .Set(v => v.UpdatedBy, variable.UpdatedBy)
            .Set(v => v.Version, nextVersion);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        variable.Version = nextVersion;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Variable>.Filter.And(
            Builders<Variable>.Filter.Eq(v => v.Id, id),
            Builders<Variable>.Filter.Eq(v => v.Version, expectedVersion));

        var result = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

    public async Task<IReadOnlyList<Variable>> GetGlobalVariablesForGroupAsync(Guid? groupId, CancellationToken cancellationToken = default)
    {
        // Returns global-scope variables where GroupId matches or GroupId is null (ungrouped globals)
        var filter = Builders<Variable>.Filter.And(
            Builders<Variable>.Filter.Eq(v => v.Scope, VariableScope.Global),
            Builders<Variable>.Filter.Or(
                Builders<Variable>.Filter.Eq(v => v.GroupId, groupId),
                Builders<Variable>.Filter.Eq(v => v.GroupId, null)));

        return await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Variable>> GetProjectVariablesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Variable>.Filter.And(
            Builders<Variable>.Filter.Eq(v => v.Scope, VariableScope.Project),
            Builders<Variable>.Filter.Eq(v => v.ProjectId, projectId));

        return await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsReferencedAsync(Guid variableId, CancellationToken cancellationToken = default)
    {
        // Check if any config entry references this variable by name via {{variableName}} pattern.
        // First retrieve the variable to get its name.
        var variable = await _collection.Find(v => v.Id == variableId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (variable is null)
        {
            return false;
        }

        var configEntries = _context.GetCollection<ConfigEntry>(CollectionNames.ConfigEntries);
        var pattern = $"{{{{{variable.Name}}}}}";
        var filter = Builders<ConfigEntry>.Filter.ElemMatch(
            e => e.Values,
            Builders<ScopedValue>.Filter.Regex(sv => sv.Value, new BsonRegularExpression(Regex.Escape(pattern))));

        var count = await configEntries.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return count > 0;
    }

    private static FilterDefinition<Variable> BuildEntityFilter(VariableListQuery query)
    {
        var filters = new List<FilterDefinition<Variable>>();

        if (query.Scope.HasValue)
        {
            filters.Add(Builders<Variable>.Filter.Eq(v => v.Scope, query.Scope.Value));
        }

        if (query.GroupId.HasValue)
        {
            filters.Add(Builders<Variable>.Filter.Eq(v => v.GroupId, query.GroupId.Value));
        }

        if (query.ProjectId.HasValue)
        {
            filters.Add(Builders<Variable>.Filter.Eq(v => v.ProjectId, query.ProjectId.Value));
        }

        return filters.Count == 0
            ? FilterDefinition<Variable>.Empty
            : Builders<Variable>.Filter.And(filters);
    }

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "name", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(Variable variable, string sortField) => sortField switch
    {
        "name" => variable.Name,
        "createdAt" => variable.CreatedAt,
        "updatedAt" => variable.UpdatedAt,
        "version" => variable.Version,
        "id" => variable.Id,
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