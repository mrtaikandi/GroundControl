using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly IMongoCollection<RefreshToken> _collection;

    public RefreshTokenStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.GetCollection<RefreshToken>(CollectionNames.RefreshTokens);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return await _collection.Find(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        return _collection.InsertOneAsync(token, cancellationToken: cancellationToken);
    }

    public async Task RevokeAsync(Guid id, DateTimeOffset revokedAt, Guid? replacedByTokenId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RefreshToken>.Filter.Eq(t => t.Id, id);
        var update = Builders<RefreshToken>.Update
            .Set(t => t.RevokedAt, revokedAt)
            .Set(t => t.ReplacedByTokenId, replacedByTokenId);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RefreshToken>.Filter.And(
            Builders<RefreshToken>.Filter.Eq(t => t.FamilyId, familyId),
            Builders<RefreshToken>.Filter.Eq(t => t.RevokedAt, null));

        var update = Builders<RefreshToken>.Update.Set(t => t.RevokedAt, revokedAt);

        await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(t => t.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}