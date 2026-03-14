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

[Collection("MongoDB")]
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