using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class SnapshotsHandlerTests : ApiHandlerTestBase
{
    public SnapshotsHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PublishSnapshot_WithValidProject_Returns201WithSnapshotSummary()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");

        var request = new PublishSnapshotRequest { Description = "Initial publish" };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            request,
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var summary = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(response, TestCancellationToken);
        summary.ProjectId.ShouldBe(project.Id);
        summary.SnapshotVersion.ShouldBe(1);
        summary.EntryCount.ShouldBe(1);
        summary.Description.ShouldBe("Initial publish");
        summary.PublishedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task PublishSnapshot_NonExistentProject_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new PublishSnapshotRequest { Description = "test" };
        var fakeProjectId = Guid.CreateVersion7();

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{fakeProjectId}/snapshots",
            request,
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSnapshot_WithSensitiveValues_ReturnsMaskedByDefault()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, ConfigEntryOwnerType.Project, value: "s3cret!", isSensitive: true);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);

        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var published = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/snapshots/{published.Id}",
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = await ReadRequiredJsonAsync<SnapshotResponse>(response, TestCancellationToken);

        var sensitiveEntry = snapshot.Entries.First(e => e.Key == "db.password");
        sensitiveEntry.IsSensitive.ShouldBeTrue();
        sensitiveEntry.Values.ShouldAllBe(v => v.Value == "***");

        var normalEntry = snapshot.Entries.First(e => e.Key == "app.name");
        normalEntry.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task GetSnapshot_WithDecryptAndPermission_ReturnsDecryptedValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, ConfigEntryOwnerType.Project, value: "s3cret!", isSensitive: true);

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);

        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var published = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);

        // Act — NoAuth gives all permissions, so decrypt=true should work
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/snapshots/{published.Id}?decrypt=true",
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = await ReadRequiredJsonAsync<SnapshotResponse>(response, TestCancellationToken);

        var sensitiveEntry = snapshot.Entries.First(e => e.Key == "db.password");
        sensitiveEntry.Values.ShouldContain(v => v.Value == "s3cret!");
    }

    [Fact]
    public async Task GetSnapshot_NonExistentSnapshot_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        var fakeSnapshotId = Guid.CreateVersion7();

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/snapshots/{fakeSnapshotId}",
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateSnapshot_PreviousSnapshot_ChangesActiveAndNotifies()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        // Publish two snapshots
        var publish1 = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "v1" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        publish1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var snapshot1 = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publish1, TestCancellationToken);

        var publish2 = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "v2" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        publish2.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act — activate the first snapshot (rollback)
        var activateResponse = await apiClient.PostAsync(
            $"/api/projects/{project.Id}/snapshots/{snapshot1.Id}/activate",
            null,
            TestCancellationToken);

        // Assert
        activateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updatedProject = await ReadRequiredJsonAsync<ProjectResponse>(activateResponse, TestCancellationToken);
        updatedProject.ActiveSnapshotId.ShouldBe(snapshot1.Id);
    }

    [Fact]
    public async Task ActivateSnapshot_AlreadyActiveSnapshot_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);

        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var snapshot = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);

        // Act — activate the snapshot that is already active
        var activateResponse = await apiClient.PostAsync(
            $"/api/projects/{project.Id}/snapshots/{snapshot.Id}/activate",
            null,
            TestCancellationToken);

        // Assert
        activateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ActivateSnapshot_NonExistentSnapshot_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        var fakeSnapshotId = Guid.CreateVersion7();

        // Act
        var activateResponse = await apiClient.PostAsync(
            $"/api/projects/{project.Id}/snapshots/{fakeSnapshotId}/activate",
            null,
            TestCancellationToken);

        // Assert
        activateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublishSnapshot_WithMatchingExpectedHash_Returns201()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");

        var previewResponse = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(previewResponse, TestCancellationToken);

        // Act
        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "with hash", ExpectedHash = preview.DiffHash },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var summary = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);
        summary.SnapshotVersion.ShouldBe(preview.NextVersion);
    }

    [Fact]
    public async Task PublishSnapshot_WithMismatchingExpectedHash_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");

        // Act
        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "stale", ExpectedHash = new string('0', 64) },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await publishResponse.Content.ReadAsStringAsync(TestCancellationToken);
        body.ShouldContain("preview");
    }

    [Fact]
    public async Task PublishSnapshot_WithoutExpectedHash_Returns201_RegressionGuard()
    {
        // Arrange — guards the existing CLI/API contract: callers that don't supply ExpectedHash
        // continue to publish unchanged. This is the path the CLI and any external integrations use.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");

        // Act
        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "no hash" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PublishSnapshot_AfterEntryChange_RejectsStaleHash()
    {
        // Arrange — simulates the race we built ExpectedHash to catch: user previews, another user
        // edits an entry, original user clicks Publish with the now-stale hash.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        var entryResponse = await apiClient.PostAsJsonAsync("/api/config-entries", new CreateConfigEntryRequest
        {
            Key = "app.name",
            OwnerId = project.Id,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "Original" }],
        }, WebJsonSerializerOptions, TestCancellationToken);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var entry = await ReadRequiredJsonAsync<ConfigEntryResponse>(entryResponse, TestCancellationToken);

        var previewResponse = await apiClient.PostAsync($"/api/projects/{project.Id}/snapshots/preview", content: null, TestCancellationToken);
        var preview = await ReadRequiredJsonAsync<PreviewSnapshotResponse>(previewResponse, TestCancellationToken);

        using (var update = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{entry.Id}")
        {
            Content = JsonContent.Create(new UpdateConfigEntryRequest
            {
                Key = entry.Key,
                ValueType = entry.ValueType,
                Values = [new ScopedValueRequest { Value = "Changed" }],
                IsSensitive = entry.IsSensitive,
            }, options: WebJsonSerializerOptions),
        })
        {
            update.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{entry.Version}\""));
            var updateResponse = await apiClient.SendAsync(update, TestCancellationToken);
            updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "stale", ExpectedHash = preview.DiffHash },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListSnapshots_ReturnsNewestFirstWithSummaryOnly()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "v1" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest { Description = "v2" },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/snapshots",
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestCancellationToken));

        var result = await ReadRequiredJsonAsync<PaginatedResponse<SnapshotSummaryResponse>>(response, TestCancellationToken);
        result.TotalCount.ShouldBe(2);
        result.Data.Count.ShouldBe(2);
        result.Data[0].SnapshotVersion.ShouldBeGreaterThan(result.Data[1].SnapshotVersion);
        result.Data[0].Description.ShouldBe("v2");
        result.Data[1].Description.ShouldBe("v1");
    }

    #region Test Helpers

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient apiClient, List<Guid>? templateIds = null)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
            TemplateIds = templateIds,
        };

        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient)
    {
        var request = new CreateTemplateRequest
        {
            Name = $"Template-{Guid.CreateVersion7():N}",
            Description = "Test template",
        };

        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken);
        template.ShouldNotBeNull();

        return template;
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        ConfigEntryOwnerType ownerType,
        string value = "default",
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ownerType,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion
}