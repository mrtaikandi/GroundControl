using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class RoleStore : IRoleStore
{
    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<Role> _roleCollection;
    private readonly IMongoCollection<User> _userCollection;

    public RoleStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _roleCollection = context.GetCollection<Role>(CollectionNames.Roles);
        _userCollection = context.GetCollection<User>(CollectionNames.Users);
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _roleCollection.Find(role => role.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var filter = Builders<Role>.Filter.Eq(role => role.Name, name);
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _roleCollection.Find(filter, options).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Role>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _roleCollection
            .Find(FilterDefinition<Role>.Empty, options)
            .SortBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        return _roleCollection.InsertOneAsync(role, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsync(Role role, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(entity => entity.Id, role.Id),
            Builders<Role>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<Role>.Update
            .Set(entity => entity.Name, role.Name)
            .Set(entity => entity.Description, role.Description)
            .Set(entity => entity.Permissions, role.Permissions)
            .Set(entity => entity.UpdatedAt, role.UpdatedAt)
            .Set(entity => entity.UpdatedBy, role.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _roleCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        role.Version = nextVersion;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(entity => entity.Id, id),
            Builders<Role>.Filter.Eq(entity => entity.Version, expectedVersion));

        var result = await _roleCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

    public async Task<bool> IsReferencedByUsersAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.ElemMatch(user => user.Grants, grant => grant.RoleId == roleId);
        var count = await _userCollection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        return count > 0;
    }
}