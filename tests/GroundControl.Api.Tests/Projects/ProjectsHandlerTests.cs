using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

[Collection("MongoDB")]
public sealed class ProjectsHandlerTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MongoFixture _mongoFixture;

    public ProjectsHandlerTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task PostProject_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest { Name = "My Project", Description = "A test project" };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);
        var project = await ReadProjectAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var request = new CreateProjectRequest { Name = "Team Project", GroupId = group.Id };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);
        var project = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        project.GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task PostProject_WithTemplateIds_ReturnsCreatedWithTemplateIds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template1 = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
        var template2 = await CreateTemplateAsync(apiClient, "Override Config", cancellationToken);
        var request = new CreateProjectRequest
        {
            Name = "Templated Project",
            TemplateIds = [template1.Id, template2.Id]
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);
        var project = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        project.TemplateIds.ShouldBe([template1.Id, template2.Id]);
    }

    [Fact]
    public async Task PostProject_WithNonExistentGroupId_ReturnsValidationProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest { Name = "Orphan Project", GroupId = Guid.CreateVersion7() };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await response.ReadValidationProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = new CreateProjectRequest
        {
            Name = "Bad Template Project",
            TemplateIds = [Guid.CreateVersion7()]
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await response.ReadValidationProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var request = new CreateProjectRequest { Name = "Duplicate", GroupId = group.Id };
        await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/projects"), request, WebJsonSerializerOptions, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostProject_SameNameInDifferentGroups_ReturnsBothCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group1 = await CreateGroupAsync(apiClient, "Team Alpha", cancellationToken);
        var group2 = await CreateGroupAsync(apiClient, "Team Beta", cancellationToken);

        // Act
        var response1 = await apiClient.PostAsJsonAsync(
            RelativeUri("/api/projects"),
            new CreateProjectRequest { Name = "SharedName", GroupId = group1.Id },
            WebJsonSerializerOptions,
            cancellationToken);

        var response2 = await apiClient.PostAsJsonAsync(
            RelativeUri("/api/projects"),
            new CreateProjectRequest { Name = "SharedName", GroupId = group2.Id },
            WebJsonSerializerOptions,
            cancellationToken);

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Created);
        response2.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetProject_WithExistingId_ReturnsProjectAndEntityTag()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/projects/{created.Id}"), cancellationToken);
        var project = await ReadProjectAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/projects/{Guid.CreateVersion7()}"), cancellationToken);
        var problem = await response.ReadProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        await CreateProjectAsync(apiClient, "Global Project", cancellationToken);
        await CreateProjectAsync(apiClient, "Group Project", cancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync(
            RelativeUri($"/api/projects?limit=25&sortField=name&sortOrder=asc&groupId={group.Id}"),
            cancellationToken);

        var page = await ReadPageAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(p => p.GroupId == group.Id);
        page.Data.ShouldContain(p => p.Name == "Group Project");
    }

    [Fact]
    public async Task GetProjects_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await CreateProjectAsync(apiClient, "Gamma", cancellationToken);
        await CreateProjectAsync(apiClient, "Alpha", cancellationToken);
        await CreateProjectAsync(apiClient, "Beta", cancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync(RelativeUri("/api/projects?limit=2&sortField=name&sortOrder=asc"), cancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, cancellationToken);

        firstPage.NextCursor.ShouldNotBeNull();
        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor);
        var secondResponse = await apiClient.GetAsync(
            RelativeUri($"/api/projects?limit=2&sortField=name&sortOrder=asc&after={nextCursor}"),
            cancellationToken);

        var secondPage = await ReadPageAsync(secondResponse, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "Original", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/projects/{created.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/projects/{created.Id}"));
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var project = await ReadProjectAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/projects/{created.Id}"));
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await response.ReadProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/projects/{created.Id}"));
        request.Content = JsonContent.Create(new UpdateProjectRequest { Name = "Updated" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await response.ReadProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "Doomed Project", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/projects/{created.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/projects/{created.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var missingResponse = await apiClient.GetAsync(RelativeUri($"/api/projects/{created.Id}"), cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var created = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/projects/{created.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await response.ReadProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Cascade Project", cancellationToken);
        var entry = await CreateConfigEntryAsync(apiClient, "CascadeKey", project.Id, ConfigEntryOwnerType.Project, cancellationToken);

        var getProjectResponse = await apiClient.GetAsync(RelativeUri($"/api/projects/{project.Id}"), cancellationToken);
        var etag = getProjectResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/projects/{project.Id}"));
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, cancellationToken);
        var entryResponse = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{entry.Id}"), cancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_CascadesDeleteToClients()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Client Cascade Project", cancellationToken);

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

        await clientCollection.InsertOneAsync(client, cancellationToken: cancellationToken);

        var getProjectResponse = await apiClient.GetAsync(RelativeUri($"/api/projects/{project.Id}"), cancellationToken);
        var etag = getProjectResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/projects/{project.Id}"));
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, cancellationToken);
        var remainingClients = await clientCollection
            .Find(c => c.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        remainingClients.ShouldBeEmpty();
    }

    [Fact]
    public async Task PutProjectTemplate_WithExistingTemplate_ReturnsUpdatedProject()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", cancellationToken);
        var template = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{template.Id}"),
            null,
            cancellationToken);

        var updated = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldContain(template.Id);
    }

    [Fact]
    public async Task PutProjectTemplate_WithNonExistentTemplate_ReturnsNotFound()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{Guid.CreateVersion7()}"),
            null,
            cancellationToken);

        var problem = await response.ReadProblemAsync(cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
        var project = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        await apiClient.PutAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{template.Id}"),
            null,
            cancellationToken);

        // Act
        var response = await apiClient.PutAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{template.Id}"),
            null,
            cancellationToken);

        var updated = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.Count(id => id == template.Id).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteProjectTemplate_WithExistingTemplate_ReturnsUpdatedProject()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
        var project = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        await apiClient.PutAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{template.Id}"),
            null,
            cancellationToken);

        // Act
        var response = await apiClient.DeleteAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{template.Id}"),
            cancellationToken);

        var updated = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldNotContain(template.Id);
    }

    [Fact]
    public async Task DeleteProjectTemplate_NotInList_ReturnsOkUnchanged()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "My Project", cancellationToken);

        // Act
        var response = await apiClient.DeleteAsync(
            RelativeUri($"/api/projects/{project.Id}/templates/{Guid.CreateVersion7()}"),
            cancellationToken);

        var updated = await ReadProjectAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.TemplateIds.ShouldBeEmpty();
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient apiClient,
        string name,
        CancellationToken cancellationToken,
        Guid? groupId = null)
    {
        var request = new CreateProjectRequest
        {
            Name = name,
            Description = $"{name} project",
            GroupId = groupId
        };

        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/projects"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadProjectAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template",
        };

        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/templates"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
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
                RelativeUri("/api/groups"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var group = await response.Content.ReadFromJsonAsync<GroupResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        group.ShouldNotBeNull();

        return group;
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        ConfigEntryOwnerType ownerType,
        CancellationToken cancellationToken)
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
                RelativeUri("/api/config-entries"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var entry = await response.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        entry.ShouldNotBeNull();

        return entry;
    }

    private static async Task<PaginatedResponse<ProjectResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProjectResponse>>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProjectResponse> ReadProjectAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        project.ShouldNotBeNull();

        return project;
    }

    private static Uri RelativeUri(string relativePath) => new(relativePath, UriKind.Relative);
}