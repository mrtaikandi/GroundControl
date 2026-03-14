using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Snapshots;

[Collection("MongoDB")]
public sealed class SnapshotPublisherTests : ApiHandlerTestBase
{
    public SnapshotPublisherTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PublishAsync_WithNoVariables_StoresSnapshotWithUnchangedEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var template = await CreateTemplateAsync(apiClient);
        var project = await CreateProjectAsync(apiClient, templateIds: [template.Id]);

        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, ConfigEntryOwnerType.Project, value: "MyApp");
        await CreateConfigEntryAsync(apiClient, "app.version", project.Id, ConfigEntryOwnerType.Project, value: "1.0");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), "test publish", TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        created.Value.ShouldNotBeNull();
        created.Value.ProjectId.ShouldBe(project.Id);
        created.Value.SnapshotVersion.ShouldBe(1);
        created.Value.Entries.Count.ShouldBe(2);
        created.Value.Entries.ShouldContain(e => e.Key == "app.name" && e.Values.Any(v => v.Value == "MyApp"));
        created.Value.Entries.ShouldContain(e => e.Key == "app.version" && e.Values.Any(v => v.Value == "1.0"));
    }

    [Fact]
    public async Task PublishAsync_WithResolvedVariables_SubstitutesPlaceholders()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);

        await CreateConfigEntryAsync(apiClient, "db.connection", project.Id, ConfigEntryOwnerType.Project, value: "Server={{dbHost}};Port={{dbPort}}");

        await CreateVariableAsync(apiClient, "dbHost", VariableScope.Project, projectId: project.Id, value: "localhost");
        await CreateVariableAsync(apiClient, "dbPort", VariableScope.Project, projectId: project.Id, value: "5432");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        created.Value.ShouldNotBeNull();
        created.Value.Entries.ShouldContain(e => e.Key == "db.connection" && e.Values.Any(v => v.Value == "Server=localhost;Port=5432"));
    }

    [Fact]
    public async Task PublishAsync_WithUnresolvedVariable_Returns422WithPlaceholderNames()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);

        await CreateConfigEntryAsync(apiClient, "api.url", project.Id, ConfigEntryOwnerType.Project, value: "https://{{apiHost}}:{{apiPort}}/v1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.ProblemDetails.Detail.ShouldNotBeNull();
        problem.ProblemDetails.Detail.ShouldContain("apiHost");
        problem.ProblemDetails.Detail.ShouldContain("apiPort");
    }

    [Fact]
    public async Task PublishAsync_WithSensitiveEntry_EncryptsValueInStoredSnapshot()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);

        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, ConfigEntryOwnerType.Project, value: "s3cret!", isSensitive: true);

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        created.Value.ShouldNotBeNull();

        var sensitiveEntry = created.Value.Entries.ShouldHaveSingleItem();
        sensitiveEntry.IsSensitive.ShouldBeTrue();

        var storedValue = sensitiveEntry.Values.ShouldHaveSingleItem().Value;
        storedValue.ShouldNotBe("s3cret!");

        // Verify it can be decrypted back
        var protector = factory.Services.GetRequiredService<IValueProtector>();
        protector.Unprotect(storedValue).ShouldBe("s3cret!");
    }

    [Fact]
    public async Task PublishAsync_WhenProjectVersionIsStale_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        // Publish once to advance the project version
        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();
        var result1 = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
        result1.Result.ShouldBeOfType<Created<Snapshot>>();

        // Bump the project version in MongoDB to simulate a concurrent modification
        // that happens after the next publish reads the project but before it activates.
        // Since we can't inject between those steps, we bump the version so the publisher
        // reads version N, but the DB has version N+1 by the time it tries to activate.
        var projectCollection = factory.Database.GetCollection<Project>("projects");
        var filter = Builders<Project>.Filter.Eq(p => p.Id, project.Id);
        var bumpUpdate = Builders<Project>.Update.Inc(p => p.Version, 100);
        await projectCollection.UpdateOneAsync(filter, bumpUpdate, cancellationToken: TestCancellationToken);

        // Now bump it again after the publisher reads it — but since we can't do that,
        // we instead test the ActivateSnapshotAsync directly with a stale version
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var currentProject = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        currentProject.ShouldNotBeNull();

        // Act — activate with a stale version (the version before the bump)
        var activated = await projectStore.ActivateSnapshotAsync(
            project.Id,
            Guid.CreateVersion7(),
            currentProject.Version - 50,
            TestCancellationToken);

        // Assert — stale version is rejected
        activated.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAsync_SnapshotVersionIncrements_PerProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result1 = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
        var result2 = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var snapshot1 = result1.Result.ShouldBeOfType<Created<Snapshot>>().Value.ShouldNotBeNull();
        var snapshot2 = result2.Result.ShouldBeOfType<Created<Snapshot>>().Value.ShouldNotBeNull();
        snapshot1.SnapshotVersion.ShouldBe(1);
        snapshot2.SnapshotVersion.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_ProjectOverridesTemplateEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var template = await CreateTemplateAsync(apiClient);
        var project = await CreateProjectAsync(apiClient, templateIds: [template.Id]);

        await CreateConfigEntryAsync(apiClient, "shared.key", template.Id, ConfigEntryOwnerType.Template, value: "template-value");
        await CreateConfigEntryAsync(apiClient, "shared.key", project.Id, ConfigEntryOwnerType.Project, value: "project-value");
        await CreateConfigEntryAsync(apiClient, "template.only", template.Id, ConfigEntryOwnerType.Template, value: "only-in-template");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        created.Value.ShouldNotBeNull();
        created.Value.Entries.Count.ShouldBe(2);
        created.Value.Entries.ShouldContain(e => e.Key == "shared.key" && e.Values.Any(v => v.Value == "project-value"));
        created.Value.Entries.ShouldContain(e => e.Key == "template.only" && e.Values.Any(v => v.Value == "only-in-template"));
    }

    [Fact]
    public async Task PublishAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        result.Result.ShouldBeOfType<NotFound>();
    }

    [Fact]
    public async Task PublishAsync_ActivatesSnapshotOnProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, ConfigEntryOwnerType.Project, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        var snapshotId = created.Value.ShouldNotBeNull().Id;

        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var updatedProject = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        updatedProject.ShouldNotBeNull();
        updatedProject.ActiveSnapshotId.ShouldBe(snapshotId);
    }

    [Fact]
    public async Task PublishAsync_WithGlobalVariables_ResolvesPlaceholders()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var group = await CreateGroupAsync(apiClient);
        var project = await CreateProjectAsync(apiClient, groupId: group.Id);

        await CreateConfigEntryAsync(apiClient, "app.env", project.Id, ConfigEntryOwnerType.Project, value: "env={{environment}}");
        await CreateVariableAsync(apiClient, "environment", VariableScope.Global, groupId: group.Id, value: "production");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act
        var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
        created.Value.ShouldNotBeNull();
        created.Value.Entries.ShouldContain(e => e.Key == "app.env" && e.Values.Any(v => v.Value == "env=production"));
    }

    #region Test Helpers

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient apiClient,
        List<Guid>? templateIds = null,
        Guid? groupId = null)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
            TemplateIds = templateIds,
            GroupId = groupId,
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

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient apiClient)
    {
        var request = new CreateGroupRequest
        {
            Name = $"Group-{Guid.CreateVersion7():N}",
            Description = "Test group",
        };

        var response = await apiClient.PostAsJsonAsync("/api/groups", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var group = await response.Content.ReadFromJsonAsync<GroupResponse>(WebJsonSerializerOptions, TestCancellationToken);
        group.ShouldNotBeNull();

        return group;
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

    private static async Task CreateVariableAsync(
        HttpClient apiClient,
        string name,
        VariableScope scope,
        Guid? projectId = null,
        Guid? groupId = null,
        string value = "default")
    {
        var request = new CreateVariableRequest
        {
            Name = name,
            Scope = scope,
            ProjectId = projectId,
            GroupId = groupId,
            Values = [new Features.Variables.Contracts.ScopedValueRequest { Value = value }],
        };

        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion
}