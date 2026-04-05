using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ProjectStore : IProjectStore
{
    private static readonly SortFieldMap<Project> SortFields = SortFieldMap<Project>.Build("name", b => b
        .Field("name", "name", p => p.Name, collation: true)
        .Field("createdAt", "createdAt", p => p.CreatedAt)
        .Field("updatedAt", "updatedAt", p => p.UpdatedAt)
        .Field("version", "version", p => p.Version)
        .Field("id", "_id", p => p.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Project> _collection;

    public ProjectStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _collection = context.GetCollection<Project>(CollectionNames.Projects);
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(project => project.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
    }

    public async Task CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            await _collection.InsertOneAsync(project, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateKeyException($"A project with name '{project.Name}' already exists for this group.", ex);
        }
    }

    public async Task<bool> UpdateAsync(Project project, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, project.Id),
            Builders<Project>.Filter.Eq(p => p.Version, expectedVersion));

        var update = Builders<Project>.Update
            .Set(p => p.Name, project.Name)
            .Set(p => p.Description, project.Description)
            .Set(p => p.GroupId, project.GroupId)
            .Set(p => p.TemplateIds, project.TemplateIds)
            .Set(p => p.UpdatedAt, project.UpdatedAt)
            .Set(p => p.UpdatedBy, project.UpdatedBy)
            .Set(p => p.Version, nextVersion);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        project.Version = nextVersion;
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _collection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

    public async Task<bool> ActivateSnapshotAsync(Guid projectId, Guid snapshotId, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var nextVersion = expectedVersion + 1;
        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, projectId),
            Builders<Project>.Filter.Eq(p => p.Version, expectedVersion));

        var update = Builders<Project>.Update
            .Set(p => p.ActiveSnapshotId, snapshotId)
            .Set(p => p.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(p => p.Version, nextVersion);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<Guid>> GetProjectIdsReferencingTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Project>.Filter.AnyEq(p => p.TemplateIds, templateId);
        var projection = Builders<Project>.Projection.Include(p => p.Id);

        var projects = await _collection.Find(filter).Project(projection).ToListAsync(cancellationToken).ConfigureAwait(false);
        return projects.Select(doc => doc["_id"].AsGuid).ToList();
    }

    private static FilterDefinition<Project> BuildEntityFilter(ProjectListQuery query)
    {
        var filters = new List<FilterDefinition<Project>>();

        if (query.GroupId.HasValue)
        {
            filters.Add(Builders<Project>.Filter.Eq(project => project.GroupId, query.GroupId.Value));
        }

        return filters.Count == 0
            ? FilterDefinition<Project>.Empty
            : Builders<Project>.Filter.And(filters);
    }
}