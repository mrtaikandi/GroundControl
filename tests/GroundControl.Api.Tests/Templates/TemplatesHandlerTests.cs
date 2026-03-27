using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Templates;

public sealed class TemplatesHandlerTests : ApiHandlerTestBase
{
    public TemplatesHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostTemplate_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("Base Config");

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        var template = await ReadTemplateAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        template.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/templates/{template.Id}");
        template.Name.ShouldBe("Base Config");
        template.GroupId.ShouldBeNull();
    }

    [Fact]
    public async Task PostTemplate_WithGroupId_ReturnsCreatedWithGroupId()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var request = new CreateTemplateRequest { Name = "Team Config", GroupId = group.Id };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        var template = await ReadTemplateAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        template.GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task PostTemplate_WithNonExistentGroupId_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = new CreateTemplateRequest { Name = "Orphan Config", GroupId = Guid.CreateVersion7() };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("GroupId");
        problem.Errors["GroupId"].ShouldContain(e => e.Contains("was not found"));
    }

    [Fact]
    public async Task GetTemplate_WithExistingId_ReturnsTemplateAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/templates/{createdTemplate.Id}", TestCancellationToken);
        var template = await ReadTemplateAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        template.Id.ShouldBe(createdTemplate.Id);
    }

    [Fact]
    public async Task GetTemplate_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/templates/{Guid.CreateVersion7()}", TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetTemplates_WithGlobalOnlyFilter_ReturnsOnlyGlobalTemplates()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Global Config", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Team Config", TestCancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync("/api/templates?limit=25&sortField=name&sortOrder=asc&globalOnly=true", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(t => t.GroupId == null);
        page.Data.ShouldContain(t => t.Name == "Global Config");
    }

    [Fact]
    public async Task GetTemplates_WithGroupIdFilter_ReturnsOnlyGroupTemplates()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Global Config", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Team Config", TestCancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync($"/api/templates?limit=25&sortField=name&sortOrder=asc&groupId={group.Id}", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(t => t.GroupId == group.Id);
        page.Data.ShouldContain(t => t.Name == "Team Config");
    }

    [Fact]
    public async Task GetTemplates_WithForwardAndBackwardCursorPagination_ReturnsFlattenedPaginatedResponse()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateTemplateAsync(apiClient, "Gamma", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Alpha", TestCancellationToken);
        await CreateTemplateAsync(apiClient, "Beta", TestCancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync("/api/templates?limit=2&sortField=name&sortOrder=asc", TestCancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        firstPage.NextCursor.ShouldNotBeNull();
        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor);
        var secondResponse = await apiClient.GetAsync($"/api/templates?limit=2&sortField=name&sortOrder=asc&after={nextCursor}", TestCancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync($"/api/templates?limit=2&sortField=name&sortOrder=asc&before={previousCursor}", TestCancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, TestCancellationToken);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstPage.Data.Select(t => t.Name).ShouldBe(["Alpha", "Beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.Data.Select(t => t.Name).ShouldBe(["Gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        previousPage.Data.Select(t => t.Name).ShouldBe(["Alpha", "Beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PutTemplate_WithCorrectIfMatch_ReturnsUpdatedTemplate()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/templates/{createdTemplate.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/templates/{createdTemplate.Id}");
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var template = await ReadTemplateAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        template.Version.ShouldBe(2);
        template.Name.ShouldBe("Updated Config");
    }

    [Fact]
    public async Task PutTemplate_WithoutIfMatch_ReturnsPreconditionRequiredProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/templates/{createdTemplate.Id}");
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);

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
    public async Task PutTemplate_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/templates/{createdTemplate.Id}");
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);
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
    public async Task DeleteTemplate_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/templates/{createdTemplate.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/templates/{createdTemplate.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/templates/{createdTemplate.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTemplate_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/templates/{createdTemplate.Id}");
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
    public async Task DeleteTemplate_WhenReferencedByProject_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", TestCancellationToken);
        var projectCollection = factory.Database.GetCollection<Project>("projects");
        var timestamp = DateTimeOffset.UtcNow;

        await projectCollection.InsertOneAsync(new Project
        {
            Id = Guid.CreateVersion7(),
            Name = "test-project",
            GroupId = Guid.CreateVersion7(),
            TemplateIds = [createdTemplate.Id],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/templates/{createdTemplate.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/templates/{createdTemplate.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("cannot be deleted");
        problem.Detail.ShouldContain("referenced");
    }

    private static CreateTemplateRequest CreateRequest(string name) => new()
    {
        Name = name,
        Description = $"{name} template"
    };

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, string name, CancellationToken cancellationToken, Guid? groupId = null)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template",
            GroupId = groupId
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/templates",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadTemplateAsync(response, TestCancellationToken).ConfigureAwait(false);
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

    private static async Task<PaginatedResponse<TemplateResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<TemplateResponse>>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<TemplateResponse> ReadTemplateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }
}