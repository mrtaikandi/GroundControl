using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.MongoDb.Tests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Audit;

[Collection("MongoDB")]
public sealed class MongoAuditStoreTests
{
    private readonly MongoFixture _mongoFixture;

    public MongoAuditStoreTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task ConfigureAsync_WithAuditRecordsCollection_CreatesExpectedIndexes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new AuditRecordConfiguration(context);

        // Act
        await configuration.ConfigureAsync(cancellationToken);
        using var cursor = await database.GetCollection<BsonDocument>("audit_records").Indexes.ListAsync(cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);

        // Assert
        indexes.Count.ShouldBe(5); // _id + 4 custom indexes

        var entityTypeIndex = indexes.Single(i => i["name"] == "ix_audit_records_entitytype_entityid");
        entityTypeIndex["key"]["entityType"].AsInt32.ShouldBe(1);
        entityTypeIndex["key"]["entityId"].AsInt32.ShouldBe(1);

        var groupIdIndex = indexes.Single(i => i["name"] == "ix_audit_records_groupid");
        groupIdIndex["key"]["groupId"].AsInt32.ShouldBe(1);

        var performedAtIndex = indexes.Single(i => i["name"] == "ix_audit_records_performedat");
        performedAtIndex["key"]["performedAt"].AsInt32.ShouldBe(-1);

        var compoundIndex = indexes.Single(i => i["name"] == "ix_audit_records_groupid_performedat");
        compoundIndex["key"]["groupId"].AsInt32.ShouldBe(1);
        compoundIndex["key"]["performedAt"].AsInt32.ShouldBe(-1);
    }

    [Fact]
    public async Task CreateAsync_WithValidRecord_InsertsIntoCollection()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var record = CreateAuditRecord("Scope", "Created");

        // Act
        await store.CreateAsync(record, cancellationToken);

        // Assert
        var retrieved = await store.GetByIdAsync(record.Id, cancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved.EntityType.ShouldBe("Scope");
        retrieved.Action.ShouldBe("Created");
        retrieved.Changes.Count.ShouldBe(1);
        retrieved.Changes[0].Field.ShouldBe("Name");
        retrieved.Changes[0].OldValue.ShouldBeNull();
        retrieved.Changes[0].NewValue.ShouldBe("Production");
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);

        // Act
        var result = await store.GetByIdAsync(Guid.CreateVersion7(), cancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_WithEntityTypeFilter_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Created"), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("ConfigEntry", "Updated"), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted"), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            EntityType = "Scope",
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(r => r.EntityType == "Scope");
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_WithEntityIdFilter_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var targetEntityId = Guid.CreateVersion7();
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", entityId: targetEntityId), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", entityId: Guid.CreateVersion7()), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted", entityId: targetEntityId), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            EntityId = targetEntityId,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(r => r.EntityId == targetEntityId);
    }

    [Fact]
    public async Task ListAsync_WithGroupIdFilter_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var groupA = Guid.CreateVersion7();
        var groupB = Guid.CreateVersion7();
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", groupId: groupA), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", groupId: groupB), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted", groupId: groupA), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            AccessibleGroupIds = [groupA],
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(r => r.GroupId == groupA);
    }

    [Fact]
    public async Task ListAsync_WithAccessibleGroupIds_ExcludesCrossGroupRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var groupA = Guid.CreateVersion7();
        var groupB = Guid.CreateVersion7();
        var groupC = Guid.CreateVersion7();
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", groupId: groupA), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", groupId: groupB), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted", groupId: groupC), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", groupId: null), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            AccessibleGroupIds = [groupA, groupB, null],
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldNotContain(r => r.GroupId == groupC);
    }

    [Fact]
    public async Task ListAsync_WithDateRange_ReturnsRecordsWithinRange()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", performedAt: now.AddHours(-3)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", performedAt: now.AddHours(-1)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted", performedAt: now.AddHours(1)), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            From = now.AddHours(-2),
            To = now,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(1);
        result.Items[0].Action.ShouldBe("Updated");
    }

    [Fact]
    public async Task ListAsync_WithPerformedByFilter_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", performedBy: userA), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", performedBy: userB), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Deleted", performedBy: userA), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            PerformedBy = userA,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(r => r.PerformedBy == userA);
    }

    [Fact]
    public async Task ListAsync_WithDefaultSort_ReturnsDescendingByPerformedAt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await store.CreateAsync(CreateAuditRecord("Scope", "First", performedAt: now.AddMinutes(-2)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Second", performedAt: now.AddMinutes(-1)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Third", performedAt: now), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(3);
        result.Items[0].Action.ShouldBe("Third");
        result.Items[1].Action.ShouldBe("Second");
        result.Items[2].Action.ShouldBe("First");
    }

    [Fact]
    public async Task ListAsync_WithForwardAndBackwardPagination_ReturnsExpectedPages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await store.CreateAsync(CreateAuditRecord("Scope", "First", performedAt: now.AddMinutes(-4)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Second", performedAt: now.AddMinutes(-3)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Third", performedAt: now.AddMinutes(-2)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Fourth", performedAt: now.AddMinutes(-1)), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Fifth", performedAt: now), cancellationToken);

        // Act — first page (most recent first, desc order)
        var firstPage = await store.ListAsync(new AuditListQuery
        {
            Limit = 2,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Act — second page (forward)
        var secondPage = await store.ListAsync(new AuditListQuery
        {
            Limit = 2,
            After = firstPage.NextCursor,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Act — back to first page (backward)
        var previousPage = await store.ListAsync(new AuditListQuery
        {
            Limit = 2,
            Before = secondPage.PreviousCursor,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        firstPage.Items.Select(r => r.Action).ShouldBe(["Fifth", "Fourth"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(5);

        secondPage.Items.Select(r => r.Action).ShouldBe(["Third", "Second"]);
        secondPage.NextCursor.ShouldNotBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();

        previousPage.Items.Select(r => r.Action).ShouldBe(["Fifth", "Fourth"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_WithEntityTypeAndEntityIdFilter_ReturnsEntityAuditTrail()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var targetEntityId = Guid.CreateVersion7();
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", entityId: targetEntityId), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("ConfigEntry", "Created", entityId: targetEntityId), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Updated", entityId: targetEntityId), cancellationToken);
        await store.CreateAsync(CreateAuditRecord("Scope", "Created", entityId: Guid.CreateVersion7()), cancellationToken);

        // Act
        var result = await store.ListAsync(new AuditListQuery
        {
            EntityType = "Scope",
            EntityId = targetEntityId,
            SortField = "performedAt",
            SortOrder = "desc"
        }, cancellationToken);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(r => r.EntityType == "Scope" && r.EntityId == targetEntityId);
    }

    [Fact]
    public async Task CreateAsync_WithMetadata_PersistsMetadata()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var record = CreateAuditRecord("Scope", "Created");
        record.Metadata["reason"] = "initial setup";
        record.Metadata["source"] = "api";

        // Act
        await store.CreateAsync(record, cancellationToken);

        // Assert
        var retrieved = await store.GetByIdAsync(record.Id, cancellationToken);
        retrieved.ShouldNotBeNull();
        retrieved.Metadata.Count.ShouldBe(2);
        retrieved.Metadata["reason"].ShouldBe("initial setup");
        retrieved.Metadata["source"].ShouldBe("api");
    }

    private async Task<(MongoAuditStore Store, IMongoDatabase Database)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new AuditRecordConfiguration(context);

        await configuration.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        return (new MongoAuditStore(context), database);
    }

    private static AuditRecord CreateAuditRecord(
        string entityType,
        string action,
        Guid? entityId = null,
        Guid? groupId = null,
        Guid? performedBy = null,
        DateTimeOffset? performedAt = null)
    {
        return new AuditRecord
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = entityId ?? Guid.CreateVersion7(),
            GroupId = groupId,
            Action = action,
            PerformedBy = performedBy ?? Guid.CreateVersion7(),
            PerformedAt = performedAt ?? DateTimeOffset.UtcNow,
            Changes =
            [
                new FieldChange
                {
                    Field = "Name",
                    OldValue = null,
                    NewValue = "Production"
                }
            ]
        };
    }
}