using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class PreviewSnapshotHandlerTests : ApiHandlerTestBase
{
    public PreviewSnapshotHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Preview_WithValidProject_Returns200WithMaskedSensitiveValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(response, TestCancellationToken);
        preview.ProjectId.ShouldBe(project.Id);
        preview.NextVersion.ShouldBe(1);
        preview.BsonSizeBytes.ShouldBeGreaterThan(0);
        preview.DiffHash.ShouldNotBeNullOrEmpty();
        preview.Entries.Count.ShouldBe(2);

        var sensitive = preview.Entries.First(e => e.Key == "db.password");
        sensitive.IsSensitive.ShouldBeTrue();
        sensitive.Values.ShouldAllBe(v => v.Value == "***");

        var normal = preview.Entries.First(e => e.Key == "app.name");
        normal.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task Preview_WithDecryptAndPermission_ReturnsPlaintextSensitiveValues()
    {
        // Arrange — NoAuth seed grants all permissions, including SensitiveValuesDecrypt.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview?decrypt=true", content: null, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(response, TestCancellationToken);
        var sensitive = preview.Entries.ShouldHaveSingleItem();
        sensitive.IsSensitive.ShouldBeTrue();
        sensitive.Values.ShouldContain(v => v.Value == "s3cret!");
    }

    [Fact]
    public async Task Preview_WithoutDecrypt_DoesNotWriteAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);

        var auditBefore = await ReadDecryptAuditCountAsync(apiClient);

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        var auditAfter = await ReadDecryptAuditCountAsync(apiClient);
        auditAfter.ShouldBe(auditBefore);
    }

    [Fact]
    public async Task Preview_WithDecryptAndSensitiveEntries_WritesDecryptedAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);

        var auditBefore = await ReadDecryptAuditCountAsync(apiClient);

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview?decrypt=true", content: null, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        var auditAfter = await ReadDecryptAuditCountAsync(apiClient);
        auditAfter.ShouldBe(auditBefore + 1);
    }

    [Fact]
    public async Task Preview_WithDecryptButNoSensitiveEntries_DoesNotAudit()
    {
        // Arrange — without sensitive entries, even ?decrypt=true emits no Decrypted audit
        // (consistent with GetSnapshotHandler, which only audits when sensitive data is revealed).
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var auditBefore = await ReadDecryptAuditCountAsync(apiClient);

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview?decrypt=true", content: null, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        var auditAfter = await ReadDecryptAuditCountAsync(apiClient);
        auditAfter.ShouldBe(auditBefore);
    }

    [Fact]
    public async Task Preview_NonExistentProject_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var fakeProjectId = Guid.CreateVersion7();

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{fakeProjectId}/snapshots/preview", content: null, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_WithUnresolvedVariable_Returns422WithPlaceholderName()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "api.url", project.Id, value: "https://{{apiHost}}/v1");

        // Act
        var response = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);
        body.ShouldContain("apiHost");
    }

    [Fact]
    public async Task Preview_DiffHash_IsStableAcrossCallsOnUnchangedProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        // Act
        var first = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);
        var second = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);

        // Assert
        var firstPreview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(first, TestCancellationToken);
        var secondPreview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(second, TestCancellationToken);
        secondPreview.DiffHash.ShouldBe(firstPreview.DiffHash);
    }

    [Fact]
    public async Task Preview_AfterPublish_NextVersionAdvances()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var firstResponse = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);
        var firstPreview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(firstResponse, TestCancellationToken);
        firstPreview.NextVersion.ShouldBe(1);

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);
        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        var secondResponse = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);

        // Assert
        var secondPreview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(secondResponse, TestCancellationToken);
        secondPreview.NextVersion.ShouldBe(2);
    }

    private static async Task<int> ReadDecryptAuditCountAsync(HttpClient apiClient)
    {
        var response = await apiClient.GetAsync("/api/audit-records?limit=100", TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);

        var count = 0;
        var index = 0;
        const string marker = "\"action\":\"Decrypted\"";
        while ((index = body.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }

        return count;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient apiClient)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
        };

        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task CreateConfigEntryAsync(HttpClient apiClient, string key, Guid ownerId, string value = "default", bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
