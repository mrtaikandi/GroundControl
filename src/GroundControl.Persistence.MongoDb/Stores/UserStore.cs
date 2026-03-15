using System.ComponentModel.DataAnnotations;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Pagination;
using GroundControl.Persistence.Stores;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Stores;

internal sealed class UserStore : IUserStore
{
    private readonly IMongoDbContext _context;
    private readonly IMongoCollection<User> _userCollection;

    public UserStore(IMongoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _userCollection = context.GetCollection<User>(CollectionNames.Users);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _userCollection.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var filter = Builders<User>.Filter.Eq(user => user.Username, username);
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _userCollection.Find(filter, options).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var filter = Builders<User>.Filter.Eq(user => user.Email, email);
        var options = new FindOptions
        {
            Collation = _context.DefaultCollation
        };

        return await _userCollection.Find(filter, options).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByExternalIdAsync(string externalProvider, string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(user => user.ExternalProvider, externalProvider),
            Builders<User>.Filter.Eq(user => user.ExternalId, externalId));

        return await _userCollection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<User>> ListAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sortField = NormalizeSortField(query.SortField);
        var bsonSortField = GetBsonSortField(sortField);
        query.SortField = sortField;

        var filter = MongoCursorPagination.BuildPageFilter<User>(query, bsonSortField);
        var sort = MongoCursorPagination.BuildSort<User>(query, bsonSortField);
        var findOptions = new FindOptions
        {
            Collation = GetCollation(sortField)
        };

        var items = await _userCollection
            .Find(filter, findOptions)
            .Sort(sort)
            .Limit(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = await _userCollection
            .CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MongoCursorPagination.MaterializePage(
            items,
            query,
            totalCount,
            user => GetSortValue(user, sortField),
            user => user.Id);
    }

    public Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        return _userCollection.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsync(User user, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var nextVersion = expectedVersion + 1;
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(entity => entity.Id, user.Id),
            Builders<User>.Filter.Eq(entity => entity.Version, expectedVersion));

        var update = Builders<User>.Update
            .Set(entity => entity.Username, user.Username)
            .Set(entity => entity.Email, user.Email)
            .Set(entity => entity.Grants, user.Grants)
            .Set(entity => entity.IsActive, user.IsActive)
            .Set(entity => entity.ExternalId, user.ExternalId)
            .Set(entity => entity.ExternalProvider, user.ExternalProvider)
            .Set(entity => entity.UpdatedAt, user.UpdatedAt)
            .Set(entity => entity.UpdatedBy, user.UpdatedBy)
            .Set(entity => entity.Version, nextVersion);

        var result = await _userCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ModifiedCount != 1)
        {
            return false;
        }

        user.Version = nextVersion;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(entity => entity.Id, id),
            Builders<User>.Filter.Eq(entity => entity.Version, expectedVersion));

        var result = await _userCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount == 1;
    }

    public async Task<IReadOnlyList<User>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.ElemMatch(user => user.Grants, grant => grant.Resource == groupId);
        return await _userCollection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private Collation? GetCollation(string sortField) => string.Equals(sortField, "username", StringComparison.Ordinal)
        ? _context.DefaultCollation
        : null;

    private static object GetSortValue(User user, string sortField) => sortField switch
    {
        "username" => user.Username,
        "email" => user.Email,
        "createdAt" => user.CreatedAt,
        "updatedAt" => user.UpdatedAt,
        "id" => user.Id,
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string GetBsonSortField(string sortField) => sortField switch
    {
        "username" => "username",
        "email" => "email",
        "createdAt" => "createdAt",
        "updatedAt" => "updatedAt",
        "id" => "_id",
        _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
    };

    private static string NormalizeSortField(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return "username";
        }

        return sortField.Trim() switch
        {
            var value when value.Equals("username", StringComparison.OrdinalIgnoreCase) => "username",
            var value when value.Equals("email", StringComparison.OrdinalIgnoreCase) => "email",
            var value when value.Equals("createdAt", StringComparison.OrdinalIgnoreCase) => "createdAt",
            var value when value.Equals("updatedAt", StringComparison.OrdinalIgnoreCase) => "updatedAt",
            var value when value.Equals("id", StringComparison.OrdinalIgnoreCase) => "id",
            _ => throw new ValidationException($"SortField '{sortField}' is not supported.")
        };
    }
}