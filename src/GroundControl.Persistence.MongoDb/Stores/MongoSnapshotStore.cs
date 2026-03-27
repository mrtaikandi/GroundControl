using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class MongoSnapshotStore : ISnapshotStore
{
    private readonly IMongoCollection<Snapshot> _collection;

    public MongoSnapshotStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.GetCollection<Snapshot>(CollectionNames.Snapshots);
    }

    public async Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<Snapshot>> ListAsync(SnapshotListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var pageFilter = MongoCursorPagination.BuildPageFilter<Snapshot>(query, bsonSortField);
        var entityFilter = Builders<Snapshot>.Filter.Eq(s => s.ProjectId, query.ProjectId);
        var combinedFilter = Builders<Snapshot>.Filter.And(entityFilter, pageFilter);

        var sort = MongoCursorPagination.BuildSort<Snapshot>(query, bsonSortField);

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
            s => GetSortValue(s, sortField),
            s => s.Id);
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

    private static object GetSortValue(Snapshot snapshot, string sortField) => sortField switch
    {
        "snapshotVersion" => snapshot.SnapshotVersion,
        "publishedAt" => snapshot.PublishedAt,
        "id" => snapshot.Id,
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string GetBsonSortField(string sortField) => sortField switch
    {
        "snapshotVersion" => "snapshotVersion",
        "publishedAt" => "publishedAt",
        "id" => "_id",
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string NormalizeSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return "snapshotVersion";
        }

        return sortField.Trim() switch
        {
            var value when value.Equals("snapshotVersion", StringComparison.OrdinalIgnoreCase) => "snapshotVersion",
            var value when value.Equals("publishedAt", StringComparison.OrdinalIgnoreCase) => "publishedAt",
            var value when value.Equals("id", StringComparison.OrdinalIgnoreCase) => "id",
            _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
        };
    }
}