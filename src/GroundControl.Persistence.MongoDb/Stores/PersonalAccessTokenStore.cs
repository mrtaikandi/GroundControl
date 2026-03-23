using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class PersonalAccessTokenStore : IPersonalAccessTokenStore
{
    private readonly IMongoCollection<PersonalAccessToken> _collection;

    public PersonalAccessTokenStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.GetCollection<PersonalAccessToken>(CollectionNames.PersonalAccessTokens);
    }

    public async Task<PersonalAccessToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(t => t.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PersonalAccessToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return await _collection.Find(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PersonalAccessToken>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(t => t.UserId == userId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CreateAsync(PersonalAccessToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        return _collection.InsertOneAsync(token, cancellationToken: cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PersonalAccessToken>.Filter.And(
            Builders<PersonalAccessToken>.Filter.Eq(t => t.Id, id),
            Builders<PersonalAccessToken>.Filter.Eq(t => t.IsRevoked, false));

        var update = Builders<PersonalAccessToken>.Update.Set(t => t.IsRevoked, true);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    public async Task UpdateLastUsedAsync(Guid id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PersonalAccessToken>.Filter.Eq(t => t.Id, id);
        var update = Builders<PersonalAccessToken>.Update.Set(t => t.LastUsedAt, lastUsedAt);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetActiveCountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PersonalAccessToken>.Filter.And(
            Builders<PersonalAccessToken>.Filter.Eq(t => t.UserId, userId),
            Builders<PersonalAccessToken>.Filter.Eq(t => t.IsRevoked, false));

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return (int)count;
    }
}