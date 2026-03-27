using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Users.Contracts;
using GroundControl.Api.Shared.Pagination;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Users;

public sealed class UsersHandlerTests : ApiHandlerTestBase
{
    public UsersHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostUser_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateUserRequest("testuser", "test@example.com");

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/users", request, WebJsonSerializerOptions, TestCancellationToken);
        var user = await ReadUserAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        user.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/users/{user.Id}");
        user.Username.ShouldBe("testuser");
        user.Email.ShouldBe("test@example.com");
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task PostUser_WithDuplicateEmail_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync("/api/users", CreateUserRequest("user1", "dup@example.com"), WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/users", CreateUserRequest("user2", "DUP@example.com"), WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Email");
        problem.Errors["Email"].ShouldContain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task PostUser_WithGrants_ReturnsUserWithGrants()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var roleId = Guid.CreateVersion7();
        var request = CreateUserRequest("granted-user", "granted@example.com", [new GrantDto { RoleId = roleId }]);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/users", request, WebJsonSerializerOptions, TestCancellationToken);
        var user = await ReadUserAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        user.Grants.ShouldHaveSingleItem();
        user.Grants[0].RoleId.ShouldBe(roleId);
    }

    [Fact]
    public async Task GetUser_WithExistingId_ReturnsUserAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "getuser", "getuser@example.com", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/users/{createdUser.Id}", TestCancellationToken);
        var user = await ReadUserAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        user.Id.ShouldBe(createdUser.Id);
    }

    [Fact]
    public async Task GetUser_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/users/{Guid.CreateVersion7()}", TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetUsers_WithPagination_ReturnsPaginatedResponse()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateUserAsync(apiClient, "charlie", "charlie@example.com", TestCancellationToken);
        await CreateUserAsync(apiClient, "alice", "alice@example.com", TestCancellationToken);
        await CreateUserAsync(apiClient, "bob", "bob@example.com", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync("/api/users?limit=2&sortField=username&sortOrder=asc", TestCancellationToken);
        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data[0].Username.ShouldBe("alice");
        page.Data[1].Username.ShouldBe("bob");
        page.NextCursor.ShouldNotBeNull();
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PutUser_WithCorrectIfMatch_ReturnsUpdatedUser()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "updateuser", "update@example.com", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/users/{createdUser.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{createdUser.Id}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = "updateduser",
            Email = "updated@example.com"
        }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var user = await ReadUserAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        user.Version.ShouldBe(2);
        user.Username.ShouldBe("updateduser");
        user.Email.ShouldBe("updated@example.com");
    }

    [Fact]
    public async Task PutUser_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "noifmatch", "noifmatch@example.com", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{createdUser.Id}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = "updated",
            Email = "updated@example.com"
        }, options: WebJsonSerializerOptions);

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
    public async Task PutUser_WithStaleIfMatch_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "staleuser", "stale@example.com", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{createdUser.Id}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = "updated",
            Email = "updated@example.com"
        }, options: WebJsonSerializerOptions);
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
    public async Task PutUser_WithGrants_UpdatesGrants()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "grantuser", "grantuser@example.com", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/users/{createdUser.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        var roleId = Guid.CreateVersion7();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{createdUser.Id}");
        request.Content = JsonContent.Create(new UpdateUserRequest
        {
            Username = "grantuser",
            Email = "grantuser@example.com",
            Grants = [new GrantDto { RoleId = roleId }]
        }, options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var user = await ReadUserAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        user.Grants.ShouldHaveSingleItem();
        user.Grants[0].RoleId.ShouldBe(roleId);
    }

    [Fact]
    public async Task DeleteUser_WithCorrectIfMatch_DeactivatesUser()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "deactivateuser", "deactivate@example.com", TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/users/{createdUser.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{createdUser.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var verifyResponse = await apiClient.GetAsync($"/api/users/{createdUser.Id}", TestCancellationToken);
        var verifiedUser = await ReadUserAsync(verifyResponse, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        verifiedUser.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteUser_WithStaleIfMatch_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdUser = await CreateUserAsync(apiClient, "staledelete", "staledelete@example.com", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{createdUser.Id}");
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

    private static CreateUserRequest CreateUserRequest(string username, string email, IReadOnlyList<GrantDto>? grants = null) => new()
    {
        Username = username,
        Email = email,
        Grants = grants
    };

    private static async Task<UserResponse> CreateUserAsync(HttpClient apiClient, string username, string email, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
            "/api/users",
            CreateUserRequest(username, email),
            WebJsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await ReadUserAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaginatedResponse<UserResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => await ReadRequiredJsonAsync<PaginatedResponse<UserResponse>>(response, cancellationToken).ConfigureAwait(false);

    private static async Task<UserResponse> ReadUserAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        => await ReadRequiredJsonAsync<UserResponse>(response, cancellationToken).ConfigureAwait(false);
}