using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Clients;

public sealed class ClientsHandlerTests : ApiHandlerTestBase
{
    public ClientsHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostClient_WithValidBody_ReturnsCreatedWithSecret()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var request = new CreateClientRequest { Name = "test-client" };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var created = await ReadCreateClientAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        created.Id.ShouldNotBe(Guid.Empty);
        created.ProjectId.ShouldBe(project.Id);
        created.Name.ShouldBe("test-client");
        created.IsActive.ShouldBeTrue();
        created.ClientSecret.ShouldNotBeNullOrWhiteSpace();
        created.Version.ShouldBe(1);
        response.Headers.Location.ToString().ShouldBe($"/api/projects/{project.Id}/clients/{created.Id}");
    }

    [Fact]
    public async Task PostClient_WithScopes_ReturnsCreatedWithScopes()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "staging", "prod"], TestCancellationToken);
        await CreateScopeAsync(apiClient, "region", ["us-east", "eu-west"], TestCancellationToken);

        var request = new CreateClientRequest
        {
            Name = "scoped-client",
            Scopes = new Dictionary<string, string>
            {
                ["environment"] = "prod",
                ["region"] = "us-east"
            }
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var created = await ReadCreateClientAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Scopes.ShouldContainKeyAndValue("environment", "prod");
        created.Scopes.ShouldContainKeyAndValue("region", "us-east");
    }

    [Fact]
    public async Task PostClient_SecretIsEncryptedInDatabase()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var request = new CreateClientRequest { Name = "encrypted-client" };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var created = await ReadCreateClientAsync(response, TestCancellationToken);

        // Assert
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var dbClient = await clientCollection.Find(c => c.Id == created.Id).FirstOrDefaultAsync(TestCancellationToken);
        dbClient.ShouldNotBeNull();
        dbClient.Secret.ShouldNotBe(created.ClientSecret);
        dbClient.Secret.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostClient_WithInvalidScopeDimension_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var request = new CreateClientRequest
        {
            Name = "bad-scope-client",
            Scopes = new Dictionary<string, string> { ["nonexistent"] = "value" }
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Scopes");
        problem.Errors["Scopes"].ShouldContain(e => e.Contains("was not found"));
    }

    [Fact]
    public async Task PostClient_WithInvalidScopeValue_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "staging", "prod"], TestCancellationToken);
        var request = new CreateClientRequest
        {
            Name = "bad-value-client",
            Scopes = new Dictionary<string, string> { ["environment"] = "invalid-env" }
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Scopes");
        problem.Errors["Scopes"].ShouldContain(e => e.Contains("is not allowed"));
    }

    [Fact]
    public async Task PostClient_WithNonExistentProject_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var fakeProjectId = Guid.CreateVersion7();
        var request = new CreateClientRequest { Name = "orphan-client" };

        // Act
        var response = await apiClient.PostAsJsonAsync(
            $"/api/projects/{fakeProjectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetClient_WithExistingId_ReturnsClientWithoutSecret()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "my-client", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{created.Id}", TestCancellationToken);

        var client = await ReadClientAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        client.Id.ShouldBe(created.Id);
        client.Name.ShouldBe("my-client");
    }

    [Fact]
    public async Task GetClient_ResponseDoesNotContainSecret()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "my-client", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{created.Id}", TestCancellationToken);

        var json = await response.Content.ReadAsStringAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        json.ShouldNotContain("clientSecret");
        json.ShouldNotContain("secret");
    }

    [Fact]
    public async Task GetClient_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{Guid.CreateVersion7()}", TestCancellationToken);

        var problem = await response.ReadProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetClients_ReturnsClientsForProject()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        await CreateClientAsync(apiClient, project.Id, "client-a", TestCancellationToken);
        await CreateClientAsync(apiClient, project.Id, "client-b", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients?limit=25&sortField=name&sortOrder=asc", TestCancellationToken);

        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.Select(c => c.Name).ShouldBe(["client-a", "client-b"]);
        page.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetClients_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        await CreateClientAsync(apiClient, project.Id, "alpha", TestCancellationToken);
        await CreateClientAsync(apiClient, project.Id, "beta", TestCancellationToken);
        await CreateClientAsync(apiClient, project.Id, "gamma", TestCancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients?limit=2&sortField=name&sortOrder=asc", TestCancellationToken);

        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        firstPage.NextCursor.ShouldNotBeNull();
        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor);
        var secondResponse = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients?limit=2&sortField=name&sortOrder=asc&after={nextCursor}", TestCancellationToken);

        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstPage.Data.Select(c => c.Name).ShouldBe(["alpha", "beta"]);
        firstPage.TotalCount.ShouldBe(3);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.Data.Select(c => c.Name).ShouldBe(["gamma"]);
        secondPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PutClient_WithCorrectIfMatch_ReturnsUpdatedClient()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "original", TestCancellationToken);
        var getResponse = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{created.Id}", TestCancellationToken);

        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{project.Id}/clients/{created.Id}");
        request.Content = JsonContent.Create(
            new UpdateClientRequest { Name = "updated", IsActive = false, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) },
            options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var client = await ReadClientAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        client.Version.ShouldBe(2);
        client.Name.ShouldBe("updated");
        client.IsActive.ShouldBeFalse();
        client.ExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task PutClient_WithoutIfMatch_ReturnsPreconditionRequired()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "my-client", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{project.Id}/clients/{created.Id}");
        request.Content = JsonContent.Create(
            new UpdateClientRequest { Name = "updated", IsActive = true },
            options: WebJsonSerializerOptions);

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
    public async Task PutClient_WithStaleIfMatch_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "my-client", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/projects/{project.Id}/clients/{created.Id}");
        request.Content = JsonContent.Create(
            new UpdateClientRequest { Name = "updated", IsActive = true },
            options: WebJsonSerializerOptions);
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
    public async Task DeleteClient_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "doomed-client", TestCancellationToken);
        var getResponse = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{created.Id}", TestCancellationToken);

        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}/clients/{created.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/clients/{created.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteClient_WithStaleIfMatch_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient, "Test Project", TestCancellationToken);
        var created = await CreateClientAsync(apiClient, project.Id, "my-client", TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{project.Id}/clients/{created.Id}");
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

    private static async Task<CreateClientResponse> CreateClientAsync(
        HttpClient apiClient, Guid projectId, string name, CancellationToken cancellationToken)
    {
        var request = new CreateClientRequest { Name = name };

        var response = await apiClient.PostAsJsonAsync(
                $"/api/projects/{projectId}/clients",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadCreateClientAsync(response, TestCancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateProjectRequest { Name = name, Description = $"{name} project" };

        var response = await apiClient.PostAsJsonAsync(
                "/api/projects",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<ScopeResponse> CreateScopeAsync(
        HttpClient apiClient, string dimension, List<string> allowedValues, CancellationToken cancellationToken)
    {
        var request = new CreateScopeRequest
        {
            Dimension = dimension,
            AllowedValues = allowedValues,
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/scopes",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        scope.ShouldNotBeNull();

        return scope;
    }

    private static async Task<CreateClientResponse> ReadCreateClientAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<ClientResponse> ReadClientAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var client = await response.Content.ReadFromJsonAsync<ClientResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<PaginatedResponse<ClientResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ClientResponse>>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }
}