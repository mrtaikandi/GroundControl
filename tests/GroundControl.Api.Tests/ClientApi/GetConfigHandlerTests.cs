using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroundControl.Api.Features.ClientApi.Contracts;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.ClientApi;

public sealed class GetConfigHandlerTests : ApiHandlerTestBase
{
    public GetConfigHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task GetConfig_HappyPath_Returns200WithFlatConfig()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "app.name", project.Id, value: "MyApp");
        await CreateConfigEntryAsync(adminClient, "app.version", project.Id, value: "1.0.0");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "test-client");

        // Act
        using var request = CreateAuthenticatedRequest(client);
        var response = await adminClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var config = await ReadRequiredJsonAsync<ClientConfigResponse>(response, TestCancellationToken);
        config.Data.ShouldContainKeyAndValue("app.name", "MyApp");
        config.Data.ShouldContainKeyAndValue("app.version", "1.0.0");
        config.SnapshotVersion.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetConfig_WithETag_ReturnsETagHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "key1", project.Id, value: "val1");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "etag-client");

        // Act
        using var request = CreateAuthenticatedRequest(client);
        var response = await adminClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.Tag.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetConfig_WithMatchingIfNoneMatch_Returns304()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "key1", project.Id, value: "val1");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "304-client");

        // First request to get the ETag
        using var firstRequest = CreateAuthenticatedRequest(client);
        var firstResponse = await adminClient.SendAsync(firstRequest, TestCancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = firstResponse.Headers.ETag;
        etag.ShouldNotBeNull();

        // Act — second request with If-None-Match
        using var secondRequest = CreateAuthenticatedRequest(client);
        secondRequest.Headers.IfNoneMatch.Add(etag);
        var response = await adminClient.SendAsync(secondRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task GetConfig_NoActiveSnapshot_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        var client = await CreateApiClientAsync(adminClient, project.Id, "no-snapshot-client");

        // Act
        using var request = CreateAuthenticatedRequest(client);
        var response = await adminClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConfig_ScopeResolution_ReturnsMostSpecificValue()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateScopeAsync(adminClient, "environment", ["dev", "prod"]);

        await CreateConfigEntryAsync(
            adminClient,
            "db.host",
            project.Id,
            values:
            [
                new ScopedValueRequest { Value = "default-db.example.com" },
                new ScopedValueRequest { Value = "prod-db.example.com", Scopes = new Dictionary<string, string> { ["environment"] = "prod" } },
            ]);

        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientWithScopesAsync(
            adminClient,
            project.Id,
            "scoped-client",
            new Dictionary<string, string> { ["environment"] = "prod" });

        // Act
        using var request = CreateAuthenticatedRequest(client);
        var response = await adminClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var config = await ReadRequiredJsonAsync<ClientConfigResponse>(response, TestCancellationToken);
        config.Data.ShouldContainKeyAndValue("db.host", "prod-db.example.com");
    }

    [Fact]
    public async Task GetConfig_SensitiveValue_ReturnsDecryptedPlainText()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);
        await CreateConfigEntryAsync(adminClient, "app.name", project.Id, value: "MyApp");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "sensitive-client");

        // Act
        using var request = CreateAuthenticatedRequest(client);
        var response = await adminClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var config = await ReadRequiredJsonAsync<ClientConfigResponse>(response, TestCancellationToken);
        config.Data.ShouldContainKeyAndValue("db.password", "s3cret!");
        config.Data.ShouldContainKeyAndValue("app.name", "MyApp");
    }

    [Fact]
    public async Task GetHealth_WithoutAuth_Returns200()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();

        // Act
        var response = await httpClient.GetAsync("/client/health", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConfig_WithoutAuth_Returns401()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();

        // Act
        var response = await httpClient.GetAsync("/client/config", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #region Test Helpers

    private static HttpRequestMessage CreateAuthenticatedRequest(CreateClientResponse client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", $"{client.Id}:{client.ClientSecret}");
        return request;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient httpClient)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
        };

        var response = await httpClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<CreateClientResponse> CreateApiClientAsync(HttpClient httpClient, Guid projectId, string name)
    {
        var request = new CreateClientRequest { Name = name };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<CreateClientResponse> CreateApiClientWithScopesAsync(
        HttpClient httpClient, Guid projectId, string name, Dictionary<string, string> scopes)
    {
        var request = new CreateClientRequest { Name = name, Scopes = scopes };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task<ScopeResponse> CreateScopeAsync(HttpClient httpClient, string dimension, List<string> allowedValues)
    {
        var request = new CreateScopeRequest { Dimension = dimension, AllowedValues = allowedValues };

        var response = await httpClient.PostAsJsonAsync("/api/scopes", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken);
        scope.ShouldNotBeNull();

        return scope;
    }

    private static async Task PublishSnapshotAsync(HttpClient httpClient, Guid projectId)
    {
        var request = new PublishSnapshotRequest { Description = "Test snapshot" };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/snapshots", request, WebJsonSerializerOptions, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient httpClient,
        string key,
        Guid projectId,
        string value = "default",
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = projectId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await httpClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient httpClient,
        string key,
        Guid projectId,
        List<ScopedValueRequest> values,
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = projectId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = values,
            IsSensitive = isSensitive,
        };

        var response = await httpClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion
}