using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class RoleConfiguration(IMongoDbContext context) : DocumentConfiguration<Role>(context, CollectionNames.Roles)
{
    private const string UxRolesName = "ux_roles_name";

    public override Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<Role>(
            Builders<Role>.IndexKeys.Ascending(role => role.Name),
            new CreateIndexOptions
            {
                Name = UxRolesName,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        return Collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}