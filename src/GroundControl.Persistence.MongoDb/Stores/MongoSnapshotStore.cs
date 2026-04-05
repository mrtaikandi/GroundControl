using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class MongoSnapshotStore : ISnapshotStore
{
    private static readonly SortFieldMap<Snapshot> SortFields = SortFieldMap<Snapshot>.Build("snapshotVersion", b => b
        .Field("snapshotVersion", "snapshotVersion", s => s.SnapshotVersion)
        .Field("publishedAt", "publishedAt", s => s.PublishedAt)
        .Field("id", "_id", s => s.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Snapshot> _collection;

    public MongoSnapshotStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _collection = context.GetCollection<Snapshot>(CollectionNames.Snapshots);
    }

    public async Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<Snapshot>> ListAsync(SnapshotListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = Builders<Snapshot>.Filter.Eq(s => s.ProjectId, query.ProjectId);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
    }

    public async Task CreateAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _collection.InsertOneAsync(snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<Snapshot?> GetActiveForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(s => s.ProjectId == projectId)
            .SortByDescending(s => s.SnapshotVersion)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<long> GetNextVersionAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var latest = await _collection
            .Find(s => s.ProjectId == projectId)
            .SortByDescending(s => s.SnapshotVersion)
            .Project(s => s.SnapshotVersion)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return latest + 1;
    }

    public async Task DeleteOldSnapshotsAsync(Guid projectId, int retentionCount, Guid? activeSnapshotId, CancellationToken cancellationToken = default)
    {
        if (retentionCount <= 0)
        {
            return;
        }

        var keepIds = await _collection
            .Find(s => s.ProjectId == projectId)
            .SortByDescending(s => s.SnapshotVersion)
            .Limit(retentionCount)
            .Project(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deleteFilter = Builders<Snapshot>.Filter.And(
            Builders<Snapshot>.Filter.Eq(s => s.ProjectId, projectId),
            Builders<Snapshot>.Filter.Nin(s => s.Id, keepIds));

        if (activeSnapshotId.HasValue)
        {
            deleteFilter = Builders<Snapshot>.Filter.And(
                deleteFilter,
                Builders<Snapshot>.Filter.Ne(s => s.Id, activeSnapshotId.Value));
        }

        await _collection.DeleteManyAsync(deleteFilter, cancellationToken).ConfigureAwait(false);
    }
}