using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Pagination;

/// <summary>
/// Extension methods for executing paginated queries against MongoDB collections
/// using <see cref="SortFieldMap{TEntity}"/> for sort field resolution.
/// </summary>
internal static class MongoPagedQueryExtensions
{
    extension<TEntity>(IMongoCollection<TEntity> collection)
    {
        /// <summary>
        /// Executes a paginated query with no entity-specific filter.
        /// Use for stores where <c>ListAsync</c> has no domain-specific filtering (e.g. GroupStore, ScopeStore, UserStore).
        /// </summary>
        public Task<PagedResult<TEntity>> ExecutePagedQueryAsync(ListQuery query,
            SortFieldMap<TEntity> sortFields,
            IMongoDbContext context,
            CancellationToken cancellationToken)
        {
            return collection.ExecutePagedQueryAsync(query, sortFields, context, FilterDefinition<TEntity>.Empty, cancellationToken);
        }

        /// <summary>
        /// Executes a paginated query with an entity-specific filter.
        /// The entity filter is combined with the cursor-based page filter and used for total count.
        /// </summary>
        public async Task<PagedResult<TEntity>> ExecutePagedQueryAsync(ListQuery query,
            SortFieldMap<TEntity> sortFields,
            IMongoDbContext context,
            FilterDefinition<TEntity> entityFilter,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(sortFields);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(entityFilter);

            var sortField = sortFields.Normalize(query.SortField);
            var bsonSortField = sortFields.GetBsonField(sortField);
            query.SortField = sortField;

            var pageFilter = MongoCursorPagination.BuildPageFilter<TEntity>(query, bsonSortField);
            var combinedFilter = Builders<TEntity>.Filter.And(entityFilter, pageFilter);

            var sort = MongoCursorPagination.BuildSort<TEntity>(query, bsonSortField);
            var collation = sortFields.GetCollation(sortField, context);
            var findOptions = collation is not null ? new FindOptions { Collation = collation } : null;

            var items = await collection
                .Find(combinedFilter, findOptions)
                .Sort(sort)
                .Limit(query.Limit + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var totalCount = await collection
                .CountDocumentsAsync(entityFilter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return MongoCursorPagination.MaterializePage(
                items,
                query,
                totalCount,
                entity => sortFields.GetSortValue(entity, sortField),
                entity => (Guid)sortFields.GetSortValue(entity, "id"));
        }
    }
}