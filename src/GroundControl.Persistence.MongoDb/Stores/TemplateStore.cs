using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class TemplateStore : ITemplateStore
{
    private static readonly SortFieldMap<Template> SortFields = SortFieldMap<Template>.Build("name", b => b
        .Field("name", "name", t => t.Name, collation: true)
        .Field("createdAt", "createdAt", t => t.CreatedAt)
        .Field("updatedAt", "updatedAt", t => t.UpdatedAt)
        .Field("version", "version", t => t.Version)
        .Field("id", "_id", t => t.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Project> _projectCollection;
    private readonly IMongoCollection<Template> _templateCollection;

    public TemplateStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _templateCollection = context.GetCollection<Template>(CollectionNames.Templates);
        _projectCollection = context.GetCollection<Project>(CollectionNames.Projects);
    }

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _templateCollection.Find(template => template.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<Template>> ListAsync(TemplateListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _templateCollection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
    }

    public Task CreateAsync(Template template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        return _templateCollection.InsertOneAsync(template, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsync(Template template, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Template>.Filter.And(
            Builders<Template>.Filter.Eq(entity => entity.Id, template.Id),
            Builders<Template>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<Template>.Update
            .Set(entity => entity.Name, template.Name)
            .Set(entity => entity.Description, template.Description)
            .Set(entity => entity.GroupId, template.GroupId)
            .Set(entity => entity.UpdatedAt, template.UpdatedAt)
            .Set(entity => entity.UpdatedBy, template.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _templateCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        template.Version = nextVersion;
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _templateCollection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

    public async Task<bool> IsReferencedByProjectsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Project>.Filter.AnyEq(project => project.TemplateIds, templateId);
        var count = await _projectCollection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        return count > 0;
    }

    private static FilterDefinition<Template> BuildEntityFilter(TemplateListQuery query)
    {
        var filters = new List<FilterDefinition<Template>>();

        if (query.GlobalOnly == true)
        {
            filters.Add(Builders<Template>.Filter.Eq(template => template.GroupId, null));
        }
        else if (query.GroupId.HasValue)
        {
            filters.Add(Builders<Template>.Filter.Eq(template => template.GroupId, query.GroupId.Value));
        }

        return filters.Count == 0
            ? FilterDefinition<Template>.Empty
            : Builders<Template>.Filter.And(filters);
    }
}