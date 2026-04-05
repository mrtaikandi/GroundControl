using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class GroupStore : IGroupStore
{
    private static readonly SortFieldMap<Group> SortFields = SortFieldMap<Group>.Build("name", b => b
        .Field("name", "name", g => g.Name, collation: true)
        .Field("createdAt", "createdAt", g => g.CreatedAt)
        .Field("updatedAt", "updatedAt", g => g.UpdatedAt)
        .Field("version", "version", g => g.Version)
        .Field("id", "_id", g => g.Id));

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

    public Task<PagedResult<Group>> ListAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _groupCollection.ExecutePagedQueryAsync(query, SortFields, _context, cancellationToken);
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

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _groupCollection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

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
}