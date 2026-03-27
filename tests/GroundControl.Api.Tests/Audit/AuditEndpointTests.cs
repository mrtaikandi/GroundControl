using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Audit.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Audit;

[Collection("MongoDB")]
public sealed class AuditEndpointTests : ApiHandlerTestBase
{
    public AuditEndpointTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task ListAuditRecords_WithNoFilters_ReturnsAllRecords()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var groupId = Guid.CreateVersion7();

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", groupId: groupId, action: "Created"),
            CreateAuditRecord("Template", groupId: groupId, action: "Updated"),
            CreateAuditRecord("ConfigEntry", groupId: null, action: "Deleted"));

        // Act
        var response = await apiClient.GetAsync("/api/audit-records", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(3);
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task ListAuditRecords_FilteredByEntityType_ReturnsOnlyMatchingType()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", action: "Created"),
            CreateAuditRecord("Template", action: "Created"),
            CreateAuditRecord("Scope", action: "Updated"));

        // Act
        var response = await apiClient.GetAsync("/api/audit-records?entityType=Scope", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(r => r.EntityType == "Scope");
    }

    [Fact]
    public async Task ListAuditRecords_FilteredByEntityId_ReturnsOnlyMatchingEntity()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var targetEntityId = Guid.CreateVersion7();

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", entityId: targetEntityId, action: "Created"),
            CreateAuditRecord("Scope", action: "Created"),
            CreateAuditRecord("Scope", entityId: targetEntityId, action: "Updated"));

        // Act
        var response = await apiClient.GetAsync($"/api/audit-records?entityId={targetEntityId}", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(r => r.EntityId == targetEntityId);
    }

    [Fact]
    public async Task ListAuditRecords_FilteredByDateRange_ReturnsRecordsWithinRange()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", action: "Created", performedAt: now.AddDays(-5)),
            CreateAuditRecord("Scope", action: "Updated", performedAt: now.AddDays(-2)),
            CreateAuditRecord("Scope", action: "Deleted", performedAt: now.AddDays(1)));

        var from = Uri.EscapeDataString(now.AddDays(-3).ToString("O"));
        var to = Uri.EscapeDataString(now.ToString("O"));

        // Act
        var response = await apiClient.GetAsync($"/api/audit-records?from={from}&to={to}", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(1);
        page.Data[0].Action.ShouldBe("Updated");
    }

    [Fact]
    public async Task ListAuditRecords_FilteredByPerformedBy_ReturnsRecordsByActor()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var targetActor = Guid.CreateVersion7();

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", action: "Created", performedBy: targetActor),
            CreateAuditRecord("Scope", action: "Updated", performedBy: Guid.CreateVersion7()),
            CreateAuditRecord("Template", action: "Created", performedBy: targetActor));

        // Act
        var response = await apiClient.GetAsync($"/api/audit-records?performedBy={targetActor}", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(r => r.PerformedBy == targetActor);
    }

    [Fact]
    public async Task ListAuditRecords_WithPagination_ReturnsCorrectPages()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        await SeedAuditRecordsAsync(factory,
            CreateAuditRecord("Scope", action: "A", performedAt: now.AddMinutes(-3)),
            CreateAuditRecord("Scope", action: "B", performedAt: now.AddMinutes(-2)),
            CreateAuditRecord("Scope", action: "C", performedAt: now.AddMinutes(-1)));

        // Act — first page (desc by performedAt, so C first)
        var firstResponse = await apiClient.GetAsync("/api/audit-records?limit=2", TestCancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        // Assert — first page
        firstPage.Data.Count.ShouldBe(2);
        firstPage.TotalCount.ShouldBe(3);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.Data[0].Action.ShouldBe("C");
        firstPage.Data[1].Action.ShouldBe("B");

        // Act — second page
        var secondResponse = await apiClient.GetAsync($"/api/audit-records?limit=2&after={firstPage.NextCursor}", TestCancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        // Assert — second page
        secondPage.Data.Count.ShouldBe(1);
        secondPage.Data[0].Action.ShouldBe("A");
        secondPage.PreviousCursor.ShouldNotBeNull();

        // Act — backward page
        var backwardResponse = await apiClient.GetAsync($"/api/audit-records?limit=2&before={secondPage.PreviousCursor}", TestCancellationToken);
        var backwardPage = await ReadPageAsync(backwardResponse, TestCancellationToken);

        // Assert — backward page returns same as first page
        backwardPage.Data.Count.ShouldBe(2);
        backwardPage.Data[0].Action.ShouldBe("C");
        backwardPage.Data[1].Action.ShouldBe("B");
    }

    [Fact]
    public async Task GetAuditRecord_WithExistingId_ReturnsRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var record = CreateAuditRecord("Scope", action: "Created",
            changes: [new FieldChange { Field = "Dimension", OldValue = null, NewValue = "region" }],
            metadata: new Dictionary<string, string> { ["source"] = "api" });

        await SeedAuditRecordsAsync(factory, record);

        // Act
        var response = await apiClient.GetAsync($"/api/audit-records/{record.Id}", TestCancellationToken);
        var result = await ReadRequiredJsonAsync<AuditRecordResponse>(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Id.ShouldBe(record.Id);
        result.EntityType.ShouldBe("Scope");
        result.Action.ShouldBe("Created");
        result.Changes.Count.ShouldBe(1);
        result.Changes[0].Field.ShouldBe("Dimension");
        result.Changes[0].NewValue.ShouldBe("region");
        result.Metadata.ShouldNotBeNull();
        result.Metadata!["source"].ShouldBe("api");
    }

    [Fact]
    public async Task GetAuditRecord_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/audit-records/{Guid.CreateVersion7()}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static AuditRecord CreateAuditRecord(
        string entityType,
        Guid? entityId = null,
        Guid? groupId = null,
        string action = "Created",
        Guid? performedBy = null,
        DateTimeOffset? performedAt = null,
        IList<FieldChange>? changes = null,
        Dictionary<string, string>? metadata = null) => new()
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = entityId ?? Guid.CreateVersion7(),
            GroupId = groupId,
            Action = action,
            PerformedBy = performedBy ?? Guid.CreateVersion7(),
            PerformedAt = performedAt ?? DateTimeOffset.UtcNow,
            Changes = changes ?? [],
            Metadata = metadata ?? [],
        };

    private static async Task SeedAuditRecordsAsync(GroundControlApiFactory factory, params AuditRecord[] records)
    {
        var collection = factory.Database.GetCollection<AuditRecord>("audit_records");
        await collection.InsertManyAsync(records);
    }

    private static async Task<PaginatedResponse<AuditRecordResponse>> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = await response.Content
            .ReadFromJsonAsync<PaginatedResponse<AuditRecordResponse>>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), cancellationToken)
            .ConfigureAwait(false);

        page.ShouldNotBeNull();
        return page!;
    }
}