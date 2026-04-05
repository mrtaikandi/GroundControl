using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class MongoAuditStore : IAuditStore
{
    private static readonly SortFieldMap<AuditRecord> SortFields = SortFieldMap<AuditRecord>.Build("performedAt", b => b
        .Field("performedAt", "performedAt", r => r.PerformedAt)
        .Field("id", "_id", r => r.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<AuditRecord> _collection;

    public MongoAuditStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
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

    public Task<PagedResult<AuditRecord>> ListAsync(AuditListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
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
}