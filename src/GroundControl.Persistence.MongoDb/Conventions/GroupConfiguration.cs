using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class GroupConfiguration(IMongoDbContext context) : DocumentConfiguration<Group>(context, CollectionNames.Groups)
{
    private const string UxGroupsName = "ux_groups_name";

    public override Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<Group>(
            Builders<Group>.IndexKeys.Ascending(group => group.Name),
            new CreateIndexOptions
            {
                Name = UxGroupsName,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        return Collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}