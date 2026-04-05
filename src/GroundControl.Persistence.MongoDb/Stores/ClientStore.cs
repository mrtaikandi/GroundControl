using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class ClientStore : IClientStore
{
    private static readonly SortFieldMap<Client> SortFields = SortFieldMap<Client>.Build("name", b => b
        .Field("name", "name", c => c.Name)
        .Field("createdAt", "createdAt", c => c.CreatedAt)
        .Field("updatedAt", "updatedAt", c => c.UpdatedAt)
        .Field("id", "_id", c => c.Id));

    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Client> _collection;

    public ClientStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _collection = context.GetCollection<Client>(CollectionNames.Clients);
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(client => client.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<PagedResult<Client>> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entityFilter = BuildEntityFilter(query);
        return _collection.ExecutePagedQueryAsync(query, SortFields, _context, entityFilter, cancellationToken);
    }

    public async Task CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        try
        {
            await _collection.InsertOneAsync(client, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateKeyException($"A client with the same key already exists.", ex);
        }
    }

    public async Task<bool> UpdateAsync(Client client, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Client>.Filter.And(
            Builders<Client>.Filter.Eq(c => c.Id, client.Id),
            Builders<Client>.Filter.Eq(c => c.Version, expectedVersion));

        var update = Builders<Client>.Update
            .Set(c => c.Name, client.Name)
            .Set(c => c.Scopes, client.Scopes)
            .Set(c => c.IsActive, client.IsActive)
            .Set(c => c.ExpiresAt, client.ExpiresAt)
            .Set(c => c.UpdatedAt, client.UpdatedAt)
            .Set(c => c.UpdatedBy, client.UpdatedBy)
            .Set(c => c.Version, nextVersion);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        client.Version = nextVersion;
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        _collection.DeleteWithVersionAsync(id, expectedVersion, cancellationToken);

    public async Task UpdateLastUsedAsync(Guid id, DateTimeOffset lastUsedAt, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Client>.Filter.Eq(c => c.Id, id);
        var update = Builders<Client>.Update.Set(c => c.LastUsedAt, lastUsedAt);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Client>.Filter.Eq(c => c.ProjectId, projectId);
        await _collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Client>> GetExpiredAndDeactivatedAsync(int gracePeriodDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-gracePeriodDays);
        var filter = Builders<Client>.Filter.And(
            Builders<Client>.Filter.Eq(c => c.IsActive, false),
            Builders<Client>.Filter.Lt(c => c.UpdatedAt, cutoff));

        return await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Client>.Filter.Eq(c => c.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    private static FilterDefinition<Client> BuildEntityFilter(ClientListQuery query)
    {
        return Builders<Client>.Filter.Eq(client => client.ProjectId, query.ProjectId);
    }
}