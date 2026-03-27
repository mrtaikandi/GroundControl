using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Projects;

public sealed class ProjectsHandlerTests : ApiHandlerTestBase
{
    public ProjectsHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostProject_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest { Name = "My Project", Description = "A test project" };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        var project = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        project.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/projects/{project.Id}");
        project.Name.ShouldBe("My Project");
        project.Description.ShouldBe("A test project");
        project.GroupId.ShouldBeNull();
        project.TemplateIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostProject_WithGroupId_ReturnsCreatedWithGroupId()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var request = new CreateProjectRequest { Name = "Team Project", GroupId = group.Id };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        var project = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        project.GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task PostProject_WithTemplateIds_ReturnsCreatedWithTemplateIds()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template1 = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var template2 = await CreateTemplateAsync(apiClient, "Override Config", TestCancellationToken);
        var request = new CreateProjectRequest
        {
            Name = "Templated Project",
            TemplateIds = [template1.Id, template2.Id]
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        var project = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        project.TemplateIds.ShouldBe([template1.Id, template2.Id]);
    }

    [Fact]
    public async Task PostProject_WithNonExistentGroupId_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest { Name = "Orphan Project", GroupId = Guid.CreateVersion7() };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("GroupId");
        problem.Errors["GroupId"].ShouldContain(e => e.Contains("was not found"));
    }

    [Fact]
    public async Task PostProject_WithNonExistentTemplateId_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest
        {
            Name = "Bad Template Project",
            TemplateIds = [Guid.CreateVersion7()]
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("TemplateIds");
        problem.Errors["TemplateIds"].ShouldContain(e => e.Contains("was not found"));
    }

    [Fact]
    public async Task PostProject_DuplicateNameWithinGroup_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var request = new CreateProjectRequest { Name = "Duplicate", GroupId = group.Id };
        await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostProject_SameNameInDifferentGroups_ReturnsBothCreated()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group1 = await CreateGroupAsync(apiClient, "Team Alpha", TestCancellationToken);
        var group2 = await CreateGroupAsync(apiClient, "Team Beta", TestCancellationToken);

        // Act
        var response1 = await apiClient.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest { Name = "SharedName", GroupId = group1.Id },
            WebJsonSerializerOptions, TestCancellationToken);

        var response2 = await apiClient.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest { Name = "SharedName", GroupId = group2.Id },
            WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetProject_WithExistingId_ReturnsProjectAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/projects/{created.Id}", TestCancellationToken);
        var project = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        project.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetProject_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/projects/{Guid.CreateVersion7()}", TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetProjects_WithGroupIdFilter_ReturnsOnlyGroupProjects()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        await CreateProjectAsync(apiClient, "Global Project", TestCancellationToken);
        await CreateProjectAsync(apiClient, "Group Project", TestCancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects?limit=25&sortField=name&sortOrder=asc&groupId={group.Id}", TestCancellationToken);

        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(p => p.GroupId == group.Id);
        page.Data.ShouldContain(p => p.Name == "Group Project");
    }

    [Fact]
    public async Task GetProjects_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateProjectAsync(apiClient, "Gamma", TestCancellationToken);
        await CreateProjectAsync(apiClient, "Alpha", TestCancellationToken);
        await CreateProjectAsync(apiClient, "Beta", TestCancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync("/api/projects?limit=2&sortField=name&sortOrder=asc", TestCancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        firstPage.NextCursor.ShouldNotBeNull();
        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor);
        var secondResponse = await apiClient.GetAsync(
            $"/api/projects?limit=2&sortField=name&sortOrder=asc&after={nextCursor}", TestCancellationToken);

        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstPage.Data.Select(p => p.Name).ShouldBe(["Alpha", "Beta"]);
        firstPage.TotalCount.ShouldBe(3);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.Data.Select(p => p.Name).ShouldBe(["Gamma"]);
        secondPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PutProject_WithCorrectIfMatch_ReturnsUpdatedProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "Original", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/projects/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{created.Id}");
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var project = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        project.Version.ShouldBe(2);
        project.Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task PutProject_WithoutIfMatch_ReturnsPreconditionRequiredProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{created.Id}");
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)428);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("If-Match header is required");
    }

    [Fact]
    public async Task PutProject_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{created.Id}");
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("Version conflict");
    }

    [Fact]
    public async Task DeleteProject_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "Doomed Project", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/projects/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{created.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/projects/{created.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{created.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("Version conflict");
    }

    [Fact]
    public async Task DeleteProject_CascadesDeleteToConfigEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Cascade Project", TestCancellationToken);
        var entry = await CreateConfigEntryAsync(apiClient, "CascadeKey", project.Id, ConfigEntryOwnerType.Project, TestCancellationToken);

        var getProjectResponse = await apiClient.GetAsync($"/api/projects/{project.Id}", TestCancellationToken);
        var etag = getProjectResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, TestCancellationToken);
        var entryResponse = await apiClient.GetAsync($"/api/config-entries/{entry.Id}", TestCancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_CascadesDeleteToClients()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Client Cascade Project", TestCancellationToken);

        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var timestamp = DateTimeOffset.UtcNow;
        var client = new Client
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "test-client",
            Secret = "test-secret",
            IsActive = true,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await clientCollection.InsertOneAsync(client, cancellationToken: TestCancellationToken);

        var getProjectResponse = await apiClient.GetAsync($"/api/projects/{project.Id}", TestCancellationToken);
        var etag = getProjectResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, TestCancellationToken);
        var remainingClients = await clientCollection
            .Find(c => c.ProjectId == project.Id)
            .ToListAsync(TestCancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        remainingClients.ShouldBeEmpty();
    }

    [Fact]
    public async Task PutProjectTemplate_WithExistingTemplate_ReturnsUpdatedProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);
        var template = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            $"/api/projects/{project.Id}/templates/{template.Id}",
            null, TestCancellationToken);

        var updated = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldContain(template.Id);
    }

    [Fact]
    public async Task PutProjectTemplate_WithNonExistentTemplate_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            $"/api/projects/{project.Id}/templates/{Guid.CreateVersion7()}",
            null,
            TestCancellationToken);

        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task PutProjectTemplate_AlreadyAdded_ReturnsOkWithoutDuplicate()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var project = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        await apiClient.PutAsync(
            $"/api/projects/{project.Id}/templates/{template.Id}",
            null, TestCancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            $"/api/projects/{project.Id}/templates/{template.Id}",
            null, TestCancellationToken);

        var updated = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.Count(id => id == template.Id).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteProjectTemplate_WithExistingTemplate_ReturnsUpdatedProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var project = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        await apiClient.PutAsync(
            $"/api/projects/{project.Id}/templates/{template.Id}",
            null, TestCancellationToken);

        // Act
        var response = await apiClient.DeleteAsync(
            $"/api/projects/{project.Id}/templates/{template.Id}", TestCancellationToken);

        var updated = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldNotContain(template.Id);
    }

    [Fact]
    public async Task DeleteProjectTemplate_NotInList_ReturnsOkUnchanged()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", TestCancellationToken);

        // Act
        var response = await apiClient.DeleteAsync(
            $"/api/projects/{project.Id}/templates/{Guid.CreateVersion7()}",
            TestCancellationToken);

        var updated = await ReadProjectAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldBeEmpty();
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient apiClient,
        string name, CancellationToken cancellationToken,
        Guid? groupId = null)
    {
        var request = new CreateProjectRequest
        {
            Name = name,
            Description = $"{name} project",
            GroupId = groupId
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/projects",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadProjectAsync(response, TestCancellationToken).ConfigureAwait(false);
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template",
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/templates",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateGroupRequest
        {
            Name = name,
            Description = $"{name} group"
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/groups",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var group = await response.Content.ReadFromJsonAsync<GroupResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        group.ShouldNotBeNull();

        return group;
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        ConfigEntryOwnerType ownerType, CancellationToken cancellationToken)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ownerType,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "default" }],
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/config-entries",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var entry = await response.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        entry.ShouldNotBeNull();

        return entry;
    }

    private static async Task<PaginatedResponse<ProjectResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProjectResponse>>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProjectResponse> ReadProjectAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        project.ShouldNotBeNull();

        return project;
    }
}