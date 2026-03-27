using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class UserConfiguration(IMongoDbContext context) : DocumentConfiguration<User>(context, CollectionNames.Users)
{
    private const string UxUsersUsername = "ux_users_username";
    private const string UxUsersEmail = "ux_users_email";
    private const string UxUsersExternalId = "ux_users_external_id";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var usernameIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(user => user.Username),
            new CreateIndexOptions
            {
                Name = UxUsersUsername,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        var emailIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions
            {
                Name = UxUsersEmail,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        var externalIdIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys
                .Ascending(user => user.ExternalProvider)
                .Ascending(user => user.ExternalId),
            new CreateIndexOptions
            {
                Name = UxUsersExternalId,
                Unique = true,
                Sparse = true
            });

        await Collection.Indexes.CreateManyAsync([usernameIndex, emailIndex, externalIdIndex], cancellationToken).ConfigureAwait(false);
    }
}