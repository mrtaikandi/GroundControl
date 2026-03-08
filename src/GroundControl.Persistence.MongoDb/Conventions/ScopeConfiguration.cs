using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class ScopeConfiguration(IMongoDbContext context) : DocumentConfiguration<Scope>(context, CollectionNames.Scopes)
{
    private const string UxScopesDimension = "ux_scopes_dimension";

    public override Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<Scope>(
            Builders<Scope>.IndexKeys.Ascending(scope => scope.Dimension),
            new CreateIndexOptions
            {
                Name = UxScopesDimension,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        return Collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}