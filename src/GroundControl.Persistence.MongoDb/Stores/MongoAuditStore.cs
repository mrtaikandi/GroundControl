using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class MongoAuditStore : IAuditStore
{
    private readonly IMongoCollection<AuditRecord> _collection;

    public MongoAuditStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.GetCollection<AuditRecord>(CollectionNames.AuditRecords);
    }

    public async Task CreateAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _collection.InsertOneAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(record => record.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<AuditRecord>> ListAsync(AuditListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var pageFilter = MongoCursorPagination.BuildPageFilter<AuditRecord>(query, bsonSortField);
        var entityFilter = BuildEntityFilter(query);
        var combinedFilter = Builders<AuditRecord>.Filter.And(entityFilter, pageFilter);

        var sort = MongoCursorPagination.BuildSort<AuditRecord>(query, bsonSortField);

        var items = await _collection
            .Find(combinedFilter)
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
            record => GetSortValue(record, sortField),
            record => record.Id);
    }

    private static FilterDefinition<AuditRecord> BuildEntityFilter(AuditListQuery query)
    {
        List<FilterDefinition<AuditRecord>> filters = [];

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            filters.Add(Builders<AuditRecord>.Filter.Eq(record => record.EntityType, query.EntityType));
        }

        if (query.EntityId.HasValue)
        {
            filters.Add(Builders<AuditRecord>.Filter.Eq(record => record.EntityId, query.EntityId.Value));
        }

        if (query.PerformedBy.HasValue)
        {
            filters.Add(Builders<AuditRecord>.Filter.Eq(record => record.PerformedBy, query.PerformedBy.Value));
        }

        if (query.From.HasValue)
        {
            filters.Add(Builders<AuditRecord>.Filter.Gte(record => record.PerformedAt, query.From.Value));
        }

        if (query.To.HasValue)
        {
            filters.Add(Builders<AuditRecord>.Filter.Lte(record => record.PerformedAt, query.To.Value));
        }

        if (query.AccessibleGroupIds is not null)
        {
            filters.Add(Builders<AuditRecord>.Filter.In(record => record.GroupId, query.AccessibleGroupIds));
        }

        return filters.Count == 0
            ? FilterDefinition<AuditRecord>.Empty
            : Builders<AuditRecord>.Filter.And(filters);
    }

    private static object GetSortValue(AuditRecord record, string sortField) => sortField switch
    {
        "performedAt" => record.PerformedAt,
        "id" => record.Id,
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string GetBsonSortField(string sortField) => sortField switch
    {
        "performedAt" => "performedAt",
        "id" => "_id",
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string NormalizeSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return "performedAt";
        }

        return sortField.Trim() switch
        {
            var value when value.Equals("performedAt", StringComparison.OrdinalIgnoreCase) => "performedAt",
            var value when value.Equals("id", StringComparison.OrdinalIgnoreCase) => "id",
            _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
        };
    }
}