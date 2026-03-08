using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class TemplateConfiguration(IMongoDbContext context) : DocumentConfiguration<Template>(context, CollectionNames.Templates)
{
    private const string UxTemplatesGroupIdName = "ux_templates_groupid_name";
    private const string IxTemplatesGroupId = "ix_templates_groupid";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var uniqueIndex = new CreateIndexModel<Template>(
            Builders<Template>.IndexKeys
                .Ascending(template => template.GroupId)
                .Ascending(template => template.Name),
            new CreateIndexOptions
            {
                Name = UxTemplatesGroupIdName,
                Unique = true,
                Collation = Context.DefaultCollation
            });

        var groupIdIndex = new CreateIndexModel<Template>(
            Builders<Template>.IndexKeys.Ascending(template => template.GroupId),
            new CreateIndexOptions
            {
                Name = IxTemplatesGroupId
            });

        await Collection.Indexes.CreateManyAsync([uniqueIndex, groupIdIndex], cancellationToken).ConfigureAwait(false);
    }
}