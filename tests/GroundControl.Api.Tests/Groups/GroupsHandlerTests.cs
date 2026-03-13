using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Tests.Infrastructure;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Groups;

[Collection("MongoDB")]
public sealed class GroupsHandlerTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MongoFixture _mongoFixture;

    public GroupsHandlerTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task PostGroup_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("Engineering");

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/groups"), request, WebJsonSerializerOptions, cancellationToken);
        var group = await ReadGroupAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        group.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/groups/{group.Id}");
        group.Name.ShouldBe("Engineering");
    }

    [Fact]
    public async Task PostGroup_WithDuplicateName_ReturnsValidationProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync(RelativeUri("/api/groups"), CreateRequest("Engineering"), WebJsonSerializerOptions, cancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/groups"), CreateRequest("engineering"), WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadValidationProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Name");
        problem.Errors["Name"].ShouldContain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task GetGroup_WithExistingId_ReturnsGroupAndEntityTag()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/groups/{createdGroup.Id}"), cancellationToken);
        var group = await ReadGroupAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        group.Id.ShouldBe(createdGroup.Id);
    }

    [Fact]
    public async Task GetGroup_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/groups/{Guid.CreateVersion7()}"), cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();

        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetGroups_WithForwardAndBackwardCursorPagination_ReturnsFlattenedPaginatedResponse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await CreateGroupAsync(apiClient, "Gamma", cancellationToken);
        await CreateGroupAsync(apiClient, "Alpha", cancellationToken);
        await CreateGroupAsync(apiClient, "Beta", cancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync(RelativeUri("/api/groups?limit=2&sortField=name&sortOrder=asc"), cancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, cancellationToken);

        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor!);
        var secondResponse = await apiClient.GetAsync(RelativeUri($"/api/groups?limit=2&sortField=name&sortOrder=asc&after={nextCursor}"), cancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, cancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync(RelativeUri($"/api/groups?limit=2&sortField=name&sortOrder=asc&before={previousCursor}"), cancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, cancellationToken);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstPage.Data.Select(group => group.Name).ShouldBe(["Alpha", "Beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.Data.Select(group => group.Name).ShouldBe(["Gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        previousPage.Data.Select(group => group.Name).ShouldBe(["Alpha", "Beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PutGroup_WithCorrectIfMatch_ReturnsUpdatedGroup()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/groups/{createdGroup.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var group = await ReadGroupAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        group.Version.ShouldBe(2);
        group.Name.ShouldBe("Platform");
    }

    [Fact]
    public async Task PutGroup_WithoutIfMatch_ReturnsPreconditionRequiredProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)428);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();

        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail.ShouldContain("If-Match header is required");
    }

    [Fact]
    public async Task PutGroup_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();

        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail.ShouldContain("Version conflict");
    }

    [Fact]
    public async Task DeleteGroup_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/groups/{createdGroup.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var missingResponse = await apiClient.GetAsync(RelativeUri($"/api/groups/{createdGroup.Id}"), cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGroup_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();

        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail.ShouldContain("Version conflict");
    }

    [Fact]
    public async Task DeleteGroup_WhenGroupHasDependents_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", cancellationToken);
        var projectCollection = factory.Database.GetCollection<Project>("projects");
        var timestamp = DateTimeOffset.UtcNow;

        await projectCollection.InsertOneAsync(new Project
        {
            Id = Guid.CreateVersion7(),
            Name = "test-project",
            GroupId = createdGroup.Id,
            TemplateIds = [],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: cancellationToken);

        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/groups/{createdGroup.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/groups/{createdGroup.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();

        var detail = problem.Detail;
        detail.ShouldNotBeNull();
        detail.ShouldContain("cannot be deleted");
        detail.ShouldContain("dependent");
    }

    private static CreateGroupRequest CreateRequest(string name) => new()
    {
        Name = name,
        Description = $"{name} group"
    };

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/groups"),
                CreateRequest(name),
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadGroupAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaginatedResponse<GroupResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<GroupResponse>>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<HttpValidationProblemDetails?> ReadValidationProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<GroupResponse> ReadGroupAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        group.ShouldNotBeNull();

        return group;
    }

    private static Uri RelativeUri(string relativePath) => new(relativePath, UriKind.Relative);
}