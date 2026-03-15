using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class ClientConfiguration(IMongoDbContext context) : DocumentConfiguration<Client>(context, CollectionNames.Clients)
{
    private const string IxClientsProjectId = "ix_clients_projectid";
    private const string IxClientsIsActiveExpiresAt = "ix_clients_isactive_expiresat";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var projectIdIndex = new CreateIndexModel<Client>(
            Builders<Client>.IndexKeys.Ascending(client => client.ProjectId),
            new CreateIndexOptions
            {
                Name = IxClientsProjectId
            });

        var isActiveExpiresAtIndex = new CreateIndexModel<Client>(
            Builders<Client>.IndexKeys
                .Ascending(client => client.IsActive)
                .Ascending(client => client.ExpiresAt),
            new CreateIndexOptions
            {
                Name = IxClientsIsActiveExpiresAt
            });

        await Collection.Indexes.CreateManyAsync(
            [projectIdIndex, isActiveExpiresAtIndex],
            cancellationToken).ConfigureAwait(false);
    }
}