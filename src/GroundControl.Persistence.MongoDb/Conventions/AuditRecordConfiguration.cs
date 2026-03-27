using GroundControl.Persistence.Contracts;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Conventions;

internal sealed class AuditRecordConfiguration(IMongoDbContext context)
    : DocumentConfiguration<AuditRecord>(context, CollectionNames.AuditRecords)
{
    private const string IxAuditRecordsEntityTypeEntityId = "ix_audit_records_entitytype_entityid";
    private const string IxAuditRecordsGroupId = "ix_audit_records_groupid";
    private const string IxAuditRecordsPerformedAt = "ix_audit_records_performedat";
    private const string IxAuditRecordsGroupIdPerformedAt = "ix_audit_records_groupid_performedat";

    public override async Task ConfigureAsync(CancellationToken cancellationToken = default)
    {
        var entityTypeEntityIdIndex = new CreateIndexModel<AuditRecord>(
            Builders<AuditRecord>.IndexKeys
                .Ascending(record => record.EntityType)
                .Ascending(record => record.EntityId),
            new CreateIndexOptions
            {
                Name = IxAuditRecordsEntityTypeEntityId
            });

        var groupIdIndex = new CreateIndexModel<AuditRecord>(
            Builders<AuditRecord>.IndexKeys.Ascending(record => record.GroupId),
            new CreateIndexOptions
            {
                Name = IxAuditRecordsGroupId
            });

        var performedAtIndex = new CreateIndexModel<AuditRecord>(
            Builders<AuditRecord>.IndexKeys.Descending(record => record.PerformedAt),
            new CreateIndexOptions
            {
                Name = IxAuditRecordsPerformedAt
            });

        var groupIdPerformedAtIndex = new CreateIndexModel<AuditRecord>(
            Builders<AuditRecord>.IndexKeys
                .Ascending(record => record.GroupId)
                .Descending(record => record.PerformedAt),
            new CreateIndexOptions
            {
                Name = IxAuditRecordsGroupIdPerformedAt
            });

        await Collection.Indexes.CreateManyAsync(
            [entityTypeEntityIdIndex, groupIdIndex, performedAtIndex, groupIdPerformedAtIndex],
            cancellationToken).ConfigureAwait(false);
    }
}