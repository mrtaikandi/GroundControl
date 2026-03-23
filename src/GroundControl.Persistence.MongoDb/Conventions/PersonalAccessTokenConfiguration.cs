using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class PersonalAccessTokenConfiguration(IMongoDbContext context)
    : DocumentConfiguration<PersonalAccessToken>(context, CollectionNames.PersonalAccessTokens)
{
    private const string UxPatTokenHash = "ux_pat_token_hash";
    private const string IxPatUserId = "ix_pat_user_id";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var tokenHashIndex = new CreateIndexModel<PersonalAccessToken>(
            Builders<PersonalAccessToken>.IndexKeys.Ascending(t => t.TokenHash),
            new CreateIndexOptions
            {
                Name = UxPatTokenHash,
                Unique = true
            });

        var userIdIndex = new CreateIndexModel<PersonalAccessToken>(
            Builders<PersonalAccessToken>.IndexKeys
                .Ascending(t => t.UserId)
                .Ascending(t => t.IsRevoked),
            new CreateIndexOptions
            {
                Name = IxPatUserId
            });

        await Collection.Indexes.CreateManyAsync([tokenHashIndex, userIdIndex], cancellationToken)
            .ConfigureAwait(false);
    }
}