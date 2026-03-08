using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class TemplateStore : ITemplateStore
{
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

    public async Task<PagedResult<Template>> ListAsync(TemplateListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var pageFilter = MongoCursorPagination.BuildPageFilter<Template>(query, bsonSortField);
        var entityFilter = BuildEntityFilter(query);
        var combinedFilter = Builders<Template>.Filter.And(entityFilter, pageFilter);

        var sort = MongoCursorPagination.BuildSort<Template>(query, bsonSortField);
        var findOptions = new FindOptions
        {
            Collation = GetCollation(sortField)
        };

        var items = await _templateCollection
            .Find(combinedFilter, findOptions)
            .Sort(sort)
            .Limit(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _templateCollection
            .CountDocumentsAsync(entityFilter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MongoCursorPagination.MaterializePage(
            items,
            query,
            totalCount,
            template => GetSortValue(template, sortField),
            template => template.Id);
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

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Template>.Filter.And(
            Builders<Template>.Filter.Eq(entity => entity.Id, id),
            Builders<Template>.Filter.Eq(entity => entity.Version, expectedVersion));

        var result = await _templateCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

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

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "name", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(Template template, string sortField) => sortField switch
    {
        "name" => template.Name,
        "createdAt" => template.CreatedAt,
        "updatedAt" => template.UpdatedAt,
        "version" => template.Version,
        "id" => template.Id,
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