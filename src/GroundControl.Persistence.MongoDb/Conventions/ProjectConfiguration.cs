using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class ProjectConfiguration(IMongoDbContext context) : DocumentConfiguration<Project>(context, CollectionNames.Projects)
{
    private const string UxProjectsGroupIdName = "ux_projects_groupid_name";
    private const string IxProjectsGroupId = "ix_projects_groupid";
    private const string IxProjectsActiveSnapshotId = "ix_projects_activesnapshotid";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var uniqueIndex = new CreateIndexModel<Project>(
            Builders<Project>.IndexKeys
                .Ascending(project => project.GroupId)
                .Ascending(project => project.Name),
            new CreateIndexOptions
            {
                Name = UxProjectsGroupIdName,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        var groupIdIndex = new CreateIndexModel<Project>(
            Builders<Project>.IndexKeys.Ascending(project => project.GroupId),
            new CreateIndexOptions
            {
                Name = IxProjectsGroupId
            });

        var activeSnapshotIdIndex = new CreateIndexModel<Project>(
            Builders<Project>.IndexKeys.Ascending(project => project.ActiveSnapshotId),
            new CreateIndexOptions
            {
                Name = IxProjectsActiveSnapshotId
            });

        await Collection.Indexes.CreateManyAsync(
            [uniqueIndex, groupIdIndex, activeSnapshotIdIndex],
            cancellationToken).ConfigureAwait(false);
    }
}