using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Groups;

public sealed class GroupsHandlerTests : ApiHandlerTestBase
{
    public GroupsHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostGroup_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("Engineering");

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/groups", request, WebJsonSerializerOptions, TestCancellationToken);
        var group = await ReadGroupAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync("/api/groups", CreateRequest("Engineering"), WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/groups", CreateRequest("engineering"), WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/groups/{createdGroup.Id}", TestCancellationToken);
        var group = await ReadGroupAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/groups/{Guid.CreateVersion7()}", TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateGroupAsync(apiClient, "Gamma", TestCancellationToken);
        await CreateGroupAsync(apiClient, "Alpha", TestCancellationToken);
        await CreateGroupAsync(apiClient, "Beta", TestCancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync("/api/groups?limit=2&sortField=name&sortOrder=asc", TestCancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor!);
        var secondResponse = await apiClient.GetAsync($"/api/groups?limit=2&sortField=name&sortOrder=asc&after={nextCursor}", TestCancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync($"/api/groups?limit=2&sortField=name&sortOrder=asc&before={previousCursor}", TestCancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/groups/{createdGroup.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/groups/{createdGroup.Id}");
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var group = await ReadGroupAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/groups/{createdGroup.Id}");
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/groups/{createdGroup.Id}");
        request.Content = JsonContent.Create(CreateRequest("Platform"), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/groups/{createdGroup.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/groups/{createdGroup.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/groups/{createdGroup.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGroup_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/groups/{createdGroup.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdGroup = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
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
        }, cancellationToken: TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/groups/{createdGroup.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/groups/{createdGroup.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
            "/api/groups",
                CreateRequest(name),
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadGroupAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaginatedResponse<GroupResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => await ReadRequiredJsonAsync<PaginatedResponse<GroupResponse>>(response, cancellationToken).ConfigureAwait(false);

    private static async Task<GroupResponse> ReadGroupAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => await ReadRequiredJsonAsync<GroupResponse>(response, cancellationToken).ConfigureAwait(false);
}