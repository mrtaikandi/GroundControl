using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class ConfigEntryConfiguration(IMongoDbContext context) : DocumentConfiguration<ConfigEntry>(context, CollectionNames.ConfigEntries)
{
    private const string UxConfigEntriesOwnerIdOwnerTypeKey = "ux_config_entries_ownerid_ownertype_key";
    private const string IxConfigEntriesOwnerIdOwnerType = "ix_config_entries_ownerid_ownertype";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var uniqueIndex = new CreateIndexModel<ConfigEntry>(
            Builders<ConfigEntry>.IndexKeys
                .Ascending(entry => entry.OwnerId)
                .Ascending(entry => entry.OwnerType)
                .Ascending(entry => entry.Key),
            new CreateIndexOptions
            {
                Name = UxConfigEntriesOwnerIdOwnerTypeKey,
                Unique = true
            });

        var ownerIndex = new CreateIndexModel<ConfigEntry>(
            Builders<ConfigEntry>.IndexKeys
                .Ascending(entry => entry.OwnerId)
                .Ascending(entry => entry.OwnerType),
            new CreateIndexOptions
            {
                Name = IxConfigEntriesOwnerIdOwnerType
            });

        await Collection.Indexes.CreateManyAsync([uniqueIndex, ownerIndex], cancellationToken).ConfigureAwait(false);
    }
}