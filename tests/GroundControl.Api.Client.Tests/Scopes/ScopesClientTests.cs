using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Client.Contracts;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Shared.Pagination;
using CreateScopeRequest = GroundControl.Api.Client.Contracts.CreateScopeRequest;
using ScopeResponse = GroundControl.Api.Client.Contracts.ScopeResponse;
using UpdateScopeRequest = GroundControl.Api.Client.Contracts.UpdateScopeRequest;

namespace GroundControl.Api.Client.Tests.Scopes;

public sealed class ScopesClientTests : ApiHandlerTestBase
{
    public ScopesClientTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateScope_ValidRequest_ReturnsCreatedResource()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var request = new CreateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev", "staging", "prod"],
            Description = "Deployment environment"
        };

        // Act
        await client.CreateScopeHandlerAsync(request, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.Created);
        handler.LastResponse.Headers.Location.ShouldNotBeNull();

        var scope = handler.DeserializeCapturedResponse<ScopeResponse>();
        scope.Dimension.ShouldBe("environment");
        scope.AllowedValues.ShouldBe(["dev", "staging", "prod"]);
        scope.Description.ShouldBe("Deployment environment");
        scope.Version.ShouldBe(1);
    }

    [Fact]
    public async Task CreateScope_MissingDimension_Returns400()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var request = new CreateScopeRequest
        {
            AllowedValues = ["dev"]
        };

        // Act
        var exception = await Should.ThrowAsync<GroundControlApiClientException>(
            () => client.CreateScopeHandlerAsync(request, TestCancellationToken));

        // Assert
        exception.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScope_DuplicateDimension_Returns400()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        await CreateScopeAsync(client, handler, "region", ["us-east", "eu-west"]);

        var duplicateRequest = new CreateScopeRequest
        {
            Dimension = "region",
            AllowedValues = ["us-east", "eu-west"]
        };

        // Act
        var exception = await Should.ThrowAsync<GroundControlApiClientException>(
            () => client.CreateScopeHandlerAsync(duplicateRequest, TestCancellationToken));

        // Assert
        exception.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetScope_ExistingId_ReturnsScope()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        // Act
        await client.GetScopeHandlerAsync(created.Id, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);
        handler.LastResponse.Headers.ETag.ShouldNotBeNull();

        var scope = handler.DeserializeCapturedResponse<ScopeResponse>();
        scope.Id.ShouldBe(created.Id);
        scope.Dimension.ShouldBe("environment");
    }

    [Fact]
    public async Task GetScope_NonExistentId_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = ApiClientFactory.Create(factory);

        // Act
        var exception = await Should.ThrowAsync<GroundControlApiClientException>(
            () => client.GetScopeHandlerAsync(Guid.CreateVersion7(), TestCancellationToken));

        // Assert
        exception.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListScopes_ReturnsPagedResults()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);
        await CreateScopeAsync(client, handler, "region", ["us", "eu"]);

        // Act
        await client.ListScopesHandlerAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);

        var page = handler.DeserializeCapturedResponse<PaginatedResponse<ScopeResponse>>();
        page.Data.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListScopes_WithPaginationParams_AppliesCorrectly()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        await CreateScopeAsync(client, handler, "alpha", ["a"]);
        await CreateScopeAsync(client, handler, "bravo", ["b"]);
        await CreateScopeAsync(client, handler, "charlie", ["c"]);

        // Act
        await client.ListScopesHandlerAsync(
            limit: 2,
            sortField: "dimension",
            sortOrder: "asc",
            cancellationToken: TestCancellationToken);

        // Assert
        var page = handler.DeserializeCapturedResponse<PaginatedResponse<ScopeResponse>>();
        page.Data.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
        page.NextCursor.ShouldNotBeNull();
        page.Data[0].Dimension.ShouldBe("alpha");
        page.Data[1].Dimension.ShouldBe("bravo");
    }

    [Fact]
    public async Task UpdateScope_WithValidIfMatch_ReturnsUpdatedResource()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        using var httpClient = factory.CreateClient();
        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev", "staging", "prod"],
            Description = "Updated"
        };

        // Act
        httpClient.DefaultRequestHeaders.Add("If-Match", $"\"{created.Version}\"");
        var response = await httpClient.PutAsJsonAsync(
            $"/api/scopes/{created.Id}", updateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await ReadRequiredJsonAsync<ScopeResponse>(response, TestCancellationToken);
        updated.AllowedValues.ShouldBe(["dev", "staging", "prod"]);
        updated.Description.ShouldBe("Updated");
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateScope_WithStaleIfMatch_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        using var httpClient = factory.CreateClient();
        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev"],
        };

        // Act
        httpClient.DefaultRequestHeaders.Add("If-Match", "\"999\"");
        var response = await httpClient.PutAsJsonAsync(
            $"/api/scopes/{created.Id}", updateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateScope_WithoutIfMatch_Returns428()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        using var httpClient = factory.CreateClient();
        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev"],
        };

        // Act
        var response = await httpClient.PutAsJsonAsync(
            $"/api/scopes/{created.Id}", updateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        ((int)response.StatusCode).ShouldBe(428);
    }

    [Fact]
    public async Task DeleteScope_WithValidIfMatch_Returns204()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        using var httpClient = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{created.Id}");
        request.Headers.Add("If-Match", $"\"{created.Version}\"");

        // Act
        var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteScope_WithStaleIfMatch_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, handler, "environment", ["dev", "prod"]);

        using var httpClient = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{created.Id}");
        request.Headers.Add("If-Match", "\"999\"");

        // Act
        var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static async Task<ScopeResponse> CreateScopeAsync(
        GroundControlClient client,
        ResponseCapturingHandler handler,
        string dimension,
        List<string> allowedValues)
    {
        var request = new CreateScopeRequest
        {
            Dimension = dimension,
            AllowedValues = allowedValues
        };

        await client.CreateScopeHandlerAsync(request, TestCancellationToken);
        return handler.DeserializeCapturedResponse<ScopeResponse>();
    }
}