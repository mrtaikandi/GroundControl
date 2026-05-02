using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;
using ConfigEntryScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;
using VariableScopedValueRequest = GroundControl.Api.Features.Variables.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Audit;

public sealed class AuditRecordingTests : ApiHandlerTestBase
{
    public AuditRecordingTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateScope_ProducesAuditRecordWithCreatedAction()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "environment", AllowedValues = ["dev", "prod"], Description = "env scope" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        response.EnsureSuccessStatusCode();
        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Scope" && r.EntityId == scope!.Id)
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        auditRecords[0].Action.ShouldBe("Created");
        auditRecords[0].PerformedBy.ShouldBe(Guid.Empty);
        auditRecords[0].GroupId.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateScope_ProducesAuditRecordWithFieldChanges()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var createResponse = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "region", AllowedValues = ["us", "eu"], Description = "region scope" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/scopes/{created!.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateScopeRequest { Dimension = "region", AllowedValues = ["us", "eu", "ap"], Description = "updated region scope" },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var updateResponse = await apiClient.SendAsync(updateRequest, TestCancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Scope" && r.EntityId == created.Id && r.Action == "Updated")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        var changes = auditRecords[0].Changes;
        changes.ShouldNotBeEmpty();
        changes.ShouldContain(c => c.Field == "AllowedValues");
        changes.ShouldContain(c => c.Field == "Description");
    }

    [Fact]
    public async Task DeleteScope_ProducesAuditRecordWithDeletedAction()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var createResponse = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "tier", AllowedValues = ["free", "paid"], Description = "tier scope" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/scopes/{created!.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{created.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, TestCancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Scope" && r.EntityId == created.Id && r.Action == "Deleted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateConfigEntry_WhenSensitive_MasksFieldChangeValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var createResponse = await apiClient.PostAsJsonAsync(
            "/api/config-entries",
            new CreateConfigEntryRequest
            {
                Key = "secret-key",
                OwnerId = Guid.CreateVersion7(),
                OwnerType = ConfigEntryOwnerType.Project,
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "secret-value-1", Scopes = [] }],
                IsSensitive = true,
                Description = "a secret",
            },
            WebJsonSerializerOptions,
            TestCancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created!.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "secret-value-2", Scopes = [] }],
                IsSensitive = true,
                Description = "a secret",
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var updateResponse = await apiClient.SendAsync(updateRequest, TestCancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.EntityId == created.Id && r.Action == "Updated")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        var valuesChange = auditRecords[0].Changes.FirstOrDefault(c => c.Field == "Values");
        valuesChange.ShouldNotBeNull();
        valuesChange.OldValue.ShouldBe("***");
        valuesChange.NewValue.ShouldBe("***");
    }

    [Fact]
    public async Task FailedCreate_DoesNotProduceAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Create a scope to trigger the validator
        await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "duplicate-dim", AllowedValues = ["a"], Description = "first" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Act - attempt duplicate (will fail validation)
        var response = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "duplicate-dim", AllowedValues = ["b"], Description = "second" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Scope" && r.Action == "Created")
            .ToListAsync(TestCancellationToken);

        // Only 1 audit record from the first successful create
        auditRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FailedUpdate_VersionConflict_DoesNotProduceAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var createResponse = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new CreateScopeRequest { Dimension = "conflict-dim", AllowedValues = ["x"], Description = "conflict test" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{created!.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateScopeRequest { Dimension = "conflict-dim", AllowedValues = ["y"], Description = "conflict test" },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var updateResponse = await apiClient.SendAsync(updateRequest, TestCancellationToken);

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Scope" && r.EntityId == created.Id && r.Action == "Updated")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreateTemplate_WithGroupId_PopulatesGroupIdInAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var groupResponse = await apiClient.PostAsJsonAsync(
            "/api/groups",
            new CreateGroupRequest { Name = "audit-test-group", Description = "test group" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupResponse>(WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var templateResponse = await apiClient.PostAsJsonAsync(
            "/api/templates",
            new CreateTemplateRequest { Name = "audit-test-template", Description = "test", GroupId = group!.Id },
            WebJsonSerializerOptions,
            TestCancellationToken);

        templateResponse.EnsureSuccessStatusCode();
        var template = await templateResponse.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Template" && r.EntityId == template!.Id && r.Action == "Created")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        auditRecords[0].GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task UpdateVariable_WhenSensitive_MasksFieldChangeValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var createResponse = await apiClient.PostAsJsonAsync(
            "/api/variables",
            new CreateVariableRequest
            {
                Name = "dbPassword",
                Scope = VariableScope.Global,
                Values = [new VariableScopedValueRequest { Value = "secret-value-1", Scopes = [] }],
                IsSensitive = true,
                Description = "a secret",
            },
            WebJsonSerializerOptions,
            TestCancellationToken);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<VariableResponse>(WebJsonSerializerOptions, TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/variables/{created!.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/variables/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateVariableRequest
            {
                Values = [new VariableScopedValueRequest { Value = "secret-value-2", Scopes = [] }],
                IsSensitive = true,
                Description = "a secret",
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var updateResponse = await apiClient.SendAsync(updateRequest, TestCancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Variable" && r.EntityId == created.Id && r.Action == "Updated")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        var valuesChange = auditRecords[0].Changes.FirstOrDefault(c => c.Field == "Values");
        valuesChange.ShouldNotBeNull();
        valuesChange.OldValue.ShouldBe("***");
        valuesChange.NewValue.ShouldBe("***");
    }
}