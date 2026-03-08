using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Tests.Infrastructure;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Templates;

[Collection("MongoDB")]
public sealed class TemplatesHandlerTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MongoFixture _mongoFixture;

    public TemplatesHandlerTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task PostTemplate_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("Base Config");

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/templates"), request, WebJsonSerializerOptions, cancellationToken);
        var template = await ReadTemplateAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var request = new CreateTemplateRequest { Name = "Team Config", GroupId = group.Id };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/templates"), request, WebJsonSerializerOptions, cancellationToken);
        var template = await ReadTemplateAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        template.GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task PostTemplate_WithNonExistentGroupId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = new CreateTemplateRequest { Name = "Orphan Config", GroupId = Guid.CreateVersion7() };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/templates"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetTemplate_WithExistingId_ReturnsTemplateAndEntityTag()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/templates/{createdTemplate.Id}"), cancellationToken);
        var template = await ReadTemplateAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/templates/{Guid.CreateVersion7()}"), cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        await CreateTemplateAsync(apiClient, "Global Config", cancellationToken);
        await CreateTemplateAsync(apiClient, "Team Config", cancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync(RelativeUri("/api/templates?limit=25&sortField=name&sortOrder=asc&globalOnly=true"), cancellationToken);
        var page = await ReadPageAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(t => t.GroupId == null);
        page.Data.ShouldContain(t => t.Name == "Global Config");
    }

    [Fact]
    public async Task GetTemplates_WithGroupIdFilter_ReturnsOnlyGroupTemplates()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        await CreateTemplateAsync(apiClient, "Global Config", cancellationToken);
        await CreateTemplateAsync(apiClient, "Team Config", cancellationToken, group.Id);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/templates?limit=25&sortField=name&sortOrder=asc&groupId={group.Id}"), cancellationToken);
        var page = await ReadPageAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.ShouldAllBe(t => t.GroupId == group.Id);
        page.Data.ShouldContain(t => t.Name == "Team Config");
    }

    [Fact]
    public async Task GetTemplates_WithForwardAndBackwardCursorPagination_ReturnsFlattenedPaginatedResponse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await CreateTemplateAsync(apiClient, "Gamma", cancellationToken);
        await CreateTemplateAsync(apiClient, "Alpha", cancellationToken);
        await CreateTemplateAsync(apiClient, "Beta", cancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync(RelativeUri("/api/templates?limit=2&sortField=name&sortOrder=asc"), cancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, cancellationToken);

        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor!);
        var secondResponse = await apiClient.GetAsync(RelativeUri($"/api/templates?limit=2&sortField=name&sortOrder=asc&after={nextCursor}"), cancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, cancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync(RelativeUri($"/api/templates?limit=2&sortField=name&sortOrder=asc&before={previousCursor}"), cancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/templates/{createdTemplate.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var template = await ReadTemplateAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Content = JsonContent.Create(new UpdateTemplateRequest { Name = "Updated Config" }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/templates/{createdTemplate.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var missingResponse = await apiClient.GetAsync(RelativeUri($"/api/templates/{createdTemplate.Id}"), cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTemplate_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdTemplate = await CreateTemplateAsync(apiClient, "Base Config", cancellationToken);
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
        }, cancellationToken: cancellationToken);

        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/templates/{createdTemplate.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/templates/{createdTemplate.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

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
                RelativeUri("/api/templates"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadTemplateAsync(response, cancellationToken).ConfigureAwait(false);
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

    private static async Task<PaginatedResponse<TemplateResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<TemplateResponse>>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<TemplateResponse> ReadTemplateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }

    private static Uri RelativeUri(string relativePath) => new(relativePath, UriKind.Relative);
}