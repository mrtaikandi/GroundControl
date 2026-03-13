using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ProjectStore : IProjectStore
{
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

    public async Task<PagedResult<Project>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var pageFilter = MongoCursorPagination.BuildPageFilter<Project>(query, bsonSortField);
        var entityFilter = BuildEntityFilter(query);
        var combinedFilter = Builders<Project>.Filter.And(entityFilter, pageFilter);

        var sort = MongoCursorPagination.BuildSort<Project>(query, bsonSortField);
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
            project => GetSortValue(project, sortField),
            project => project.Id);
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

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, id),
            Builders<Project>.Filter.Eq(p => p.Version, expectedVersion));

        var result = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

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

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "name", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(Project project, string sortField) => sortField switch
    {
        "name" => project.Name,
        "createdAt" => project.CreatedAt,
        "updatedAt" => project.UpdatedAt,
        "version" => project.Version,
        "id" => project.Id,
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