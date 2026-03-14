using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class SnapshotConfiguration(IMongoDbContext context) : DocumentConfiguration<Snapshot>(context, CollectionNames.Snapshots)
{
    private const string UxSnapshotsProjectIdSnapshotVersion = "ux_snapshots_projectid_snapshotversion";
    private const string IxSnapshotsProjectId = "ix_snapshots_projectid";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var uniqueIndex = new CreateIndexModel<Snapshot>(
            Builders<Snapshot>.IndexKeys
                .Ascending(s => s.ProjectId)
                .Descending(s => s.SnapshotVersion),
            new CreateIndexOptions
            {
                Name = UxSnapshotsProjectIdSnapshotVersion,
                Unique = true
            });

        var projectIndex = new CreateIndexModel<Snapshot>(
            Builders<Snapshot>.IndexKeys
                .Ascending(s => s.ProjectId),
            new CreateIndexOptions
            {
                Name = IxSnapshotsProjectId
            });

        await Collection.Indexes.CreateManyAsync([uniqueIndex, projectIndex], cancellationToken).ConfigureAwait(false);
    }
}