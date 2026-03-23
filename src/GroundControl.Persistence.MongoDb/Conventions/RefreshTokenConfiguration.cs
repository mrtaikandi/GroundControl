using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class RefreshTokenConfiguration(IMongoDbContext context)
    : DocumentConfiguration<RefreshToken>(context, CollectionNames.RefreshTokens)
{
    private const string UxRefreshTokensTokenHash = "ux_refresh_tokens_token_hash";
    private const string IxRefreshTokensFamilyId = "ix_refresh_tokens_family_id";
    private const string IxRefreshTokensExpiresAtTtl = "ix_refresh_tokens_expires_at_ttl";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var tokenHashIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.TokenHash),
            new CreateIndexOptions
            {
                Name = UxRefreshTokensTokenHash,
                Unique = true
            });

        var familyIdIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.FamilyId),
            new CreateIndexOptions
            {
                Name = IxRefreshTokensFamilyId
            });

        var ttlIndex = new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt),
            new CreateIndexOptions
            {
                Name = IxRefreshTokensExpiresAtTtl,
                ExpireAfter = TimeSpan.Zero
            });

        await Collection.Indexes.CreateManyAsync([tokenHashIndex, familyIdIndex, ttlIndex], cancellationToken)
            .ConfigureAwait(false);
    }
}