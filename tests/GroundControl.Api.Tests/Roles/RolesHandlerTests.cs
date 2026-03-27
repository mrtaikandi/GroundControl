using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Roles;

public sealed class RolesHandlerTests : ApiHandlerTestBase
{
    public RolesHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostRole_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("CustomRole", ["scopes:read", "groups:read"]);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/roles", request, WebJsonSerializerOptions, TestCancellationToken);
        var role = await ReadRoleAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        role.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/roles/{role.Id}");
        role.Name.ShouldBe("CustomRole");
        role.Permissions.ShouldBe(["scopes:read", "groups:read"]);
    }

    [Fact]
    public async Task PostRole_WithDuplicateName_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync("/api/roles", CreateRequest("DuplicateRole", ["scopes:read"]), WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/roles", CreateRequest("duplicaterole", ["groups:read"]), WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Name");
        problem.Errors["Name"].ShouldContain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task PostRole_WithInvalidPermission_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("BadRole", ["scopes:read", "invalid:permission"]);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/roles", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Permissions");
        problem.Errors["Permissions"].ShouldContain(e => e.Contains("invalid:permission"));
    }

    [Fact]
    public async Task GetRole_WithExistingId_ReturnsRoleAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "TestRole", ["scopes:read"], TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);
        var role = await ReadRoleAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        role.Id.ShouldBe(createdRole.Id);
    }

    [Fact]
    public async Task GetRole_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/roles/{Guid.CreateVersion7()}", TestCancellationToken);
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
    public async Task GetRoles_ReturnsAllRolesAsFlatArray()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // The seed service creates 4 default roles; add one more
        await CreateRoleAsync(apiClient, "CustomRole", ["scopes:read"], TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync("/api/roles", TestCancellationToken);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleResponse>>(WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        roles.ShouldNotBeNull();
        roles.Count.ShouldBe(5);
        roles.Select(r => r.Name).ShouldContain("Viewer");
        roles.Select(r => r.Name).ShouldContain("Editor");
        roles.Select(r => r.Name).ShouldContain("Publisher");
        roles.Select(r => r.Name).ShouldContain("Admin");
        roles.Select(r => r.Name).ShouldContain("CustomRole");
    }

    [Fact]
    public async Task PutRole_WithCorrectIfMatch_ReturnsUpdatedRole()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "OriginalRole", ["scopes:read"], TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/roles/{createdRole.Id}");
        request.Content = JsonContent.Create(CreateRequest("RenamedRole", ["scopes:read", "groups:read"]), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var role = await ReadRoleAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        role.Version.ShouldBe(2);
        role.Name.ShouldBe("RenamedRole");
        role.Permissions.ShouldBe(["scopes:read", "groups:read"]);
    }

    [Fact]
    public async Task PutRole_WithoutIfMatch_ReturnsPreconditionRequiredProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "TestRole", ["scopes:read"], TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/roles/{createdRole.Id}");
        request.Content = JsonContent.Create(CreateRequest("Updated", ["scopes:read"]), options: WebJsonSerializerOptions);

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
    public async Task PutRole_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "TestRole", ["scopes:read"], TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/roles/{createdRole.Id}");
        request.Content = JsonContent.Create(CreateRequest("Updated", ["scopes:read"]), options: WebJsonSerializerOptions);
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
    public async Task PutRole_WithInvalidPermission_ReturnsValidationProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "TestRole", ["scopes:read"], TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/roles/{createdRole.Id}");
        request.Content = JsonContent.Create(CreateRequest("Updated", ["bogus:perm"]), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Permissions");
        problem.Errors["Permissions"].ShouldContain(e => e.Contains("bogus:perm"));
    }

    [Fact]
    public async Task DeleteRole_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "ToDelete", ["scopes:read"], TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{createdRole.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRole_WhenReferencedByUser_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdRole = await CreateRoleAsync(apiClient, "ReferencedRole", ["scopes:read"], TestCancellationToken);
        var userCollection = factory.Database.GetCollection<User>("users");
        var timestamp = DateTimeOffset.UtcNow;

        await userCollection.InsertOneAsync(new User
        {
            Id = Guid.CreateVersion7(),
            Username = "testuser",
            Email = "test@example.com",
            Grants = [new Grant { RoleId = createdRole.Id }],
            IsActive = true,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/roles/{createdRole.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{createdRole.Id}");
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
    }

    [Fact]
    public async Task SeedService_CreatesDefaultRolesOnStartup()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync("/api/roles", TestCancellationToken);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleResponse>>(WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        roles.ShouldNotBeNull();
        roles.Count.ShouldBeGreaterThanOrEqualTo(4);

        var roleNames = roles.Select(r => r.Name).ToList();
        roleNames.ShouldContain("Viewer");
        roleNames.ShouldContain("Editor");
        roleNames.ShouldContain("Publisher");
        roleNames.ShouldContain("Admin");
    }

    [Fact]
    public async Task SeedService_IsIdempotent_DoesNotDuplicateRoles()
    {
        // Arrange — pre-insert a "Viewer" role before the seed service runs
        await using var factory = CreateFactory();
        var roleCollection = factory.Database.GetCollection<Role>("roles");
        var timestamp = DateTimeOffset.UtcNow;

        await roleCollection.InsertOneAsync(new Role
        {
            Id = Guid.CreateVersion7(),
            Name = "Viewer",
            Description = "Pre-existing viewer",
            Permissions = ["scopes:read"],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: TestCancellationToken);

        // Act — creating the client triggers the hosted service (seed)
        using var apiClient = factory.CreateClient();
        var response = await apiClient.GetAsync("/api/roles", TestCancellationToken);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleResponse>>(WebJsonSerializerOptions, TestCancellationToken);

        // Assert — only one Viewer should exist (the pre-inserted one)
        roles.ShouldNotBeNull();
        roles.Count(r => r.Name == "Viewer").ShouldBe(1);
        roles.Count(r => r.Name == "Editor").ShouldBe(1);
        roles.Count(r => r.Name == "Publisher").ShouldBe(1);
        roles.Count(r => r.Name == "Admin").ShouldBe(1);
    }

    private static CreateRoleRequest CreateRequest(string name, string[] permissions) => new()
    {
        Name = name,
        Description = $"{name} role",
        Permissions = permissions
    };

    private static async Task<RoleResponse> CreateRoleAsync(HttpClient apiClient, string name, string[] permissions, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
                "/api/roles",
                CreateRequest(name, permissions),
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadRoleAsync(response, TestCancellationToken).ConfigureAwait(false);
    }

    private static async Task<RoleResponse> ReadRoleAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var role = await response.Content.ReadFromJsonAsync<RoleResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        role.ShouldNotBeNull();

        return role;
    }
}