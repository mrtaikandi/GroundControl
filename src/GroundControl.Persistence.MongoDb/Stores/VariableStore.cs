using System.Text.RegularExpressions;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class VariableStore : IVariableStore
{
    private static readonly SortFieldMap<Variable> SortFields = SortFieldMap<Variable>.Build("name", b => b
        .Field("name", "name", v => v.Name, collation: true)
        .Field("createdAt", "createdAt", v => v.CreatedAt)
        .Field("updatedAt", "updatedAt", v => v.UpdatedAt)
        .Field("version", "version", v => v.Version)
        .Field("id", "_id", v => v.Id));

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

    public Task<PagedResult<Variable>> ListAsync(VariableListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
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

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _collection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

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
}