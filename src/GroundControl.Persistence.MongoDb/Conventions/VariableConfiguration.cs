using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class VariableConfiguration(IMongoDbContext context) : DocumentConfiguration<Variable>(context, CollectionNames.Variables)
{
    private const string UxVariablesScopeGroupIdName = "ux_variables_scope_groupid_name";
    private const string UxVariablesScopeProjectIdName = "ux_variables_scope_projectid_name";
    private const string IxVariablesProjectId = "ix_variables_projectid";
    private const string IxVariablesGroupId = "ix_variables_groupid";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        // Unique name within global scope + group owner
        var globalUniqueIndex = new CreateIndexModel<Variable>(
            Builders<Variable>.IndexKeys
                .Ascending(v => v.Scope)
                .Ascending(v => v.GroupId)
                .Ascending(v => v.Name),
            new CreateIndexOptions<Variable>
            {
                Name = UxVariablesScopeGroupIdName,
                Unique = true,
                Collation = Context.DefaultCollation,
                PartialFilterExpression = Builders<Variable>.Filter.Eq(v => v.Scope, VariableScope.Global)
            });

        // Unique name within project scope + project owner
        var projectUniqueIndex = new CreateIndexModel<Variable>(
            Builders<Variable>.IndexKeys
                .Ascending(v => v.Scope)
                .Ascending(v => v.ProjectId)
                .Ascending(v => v.Name),
            new CreateIndexOptions<Variable>
            {
                Name = UxVariablesScopeProjectIdName,
                Unique = true,
                Collation = Context.DefaultCollation,
                PartialFilterExpression = Builders<Variable>.Filter.Eq(v => v.Scope, VariableScope.Project)
            });

        var projectIdIndex = new CreateIndexModel<Variable>(
            Builders<Variable>.IndexKeys.Ascending(v => v.ProjectId),
            new CreateIndexOptions
            {
                Name = IxVariablesProjectId
            });

        var groupIdIndex = new CreateIndexModel<Variable>(
            Builders<Variable>.IndexKeys.Ascending(v => v.GroupId),
            new CreateIndexOptions
            {
                Name = IxVariablesGroupId
            });

        await Collection.Indexes.CreateManyAsync(
            [globalUniqueIndex, projectUniqueIndex, projectIdIndex, groupIdIndex],
            cancellationToken).ConfigureAwait(false);
    }
}