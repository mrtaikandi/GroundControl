using System.Net;
using System.Text.Json;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Pagination;
using Microsoft.Kiota.Abstractions;
using CreateScopeRequest = GroundControl.Api.Client.Models.CreateScopeRequest;
using UpdateScopeRequest = GroundControl.Api.Client.Models.UpdateScopeRequest;

namespace GroundControl.Api.Client.Tests.Scopes;

public sealed class ScopesClientTests : ApiHandlerTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ScopesClientTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateScope_ValidRequest_ReturnsCreatedResource()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var request = new CreateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev", "staging", "prod"],
            Description = "Deployment environment"
        };

        // Act
        using var stream = await client.Api.Scopes.PostAsync(request, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        handler.LastResponse.Headers.Location.ShouldNotBeNull();

        var scope = await DeserializeAsync<ScopeResponse>(stream);
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
        var (client, handler) = KiotaClientFactory.Create(factory);
        var request = new CreateScopeRequest
        {
            AllowedValues = ["dev"]
        };

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes.PostAsync(request, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScope_DuplicateDimension_Returns400()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = KiotaClientFactory.Create(factory);
        await CreateScopeAsync(client, "region", ["us-east", "eu-west"]);

        var duplicateRequest = new CreateScopeRequest
        {
            Dimension = "region",
            AllowedValues = ["us-east", "eu-west"]
        };

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes.PostAsync(duplicateRequest, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetScope_ExistingId_ReturnsScope()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);

        // Act
        using var stream = await client.Api.Scopes[created.Id].GetAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.LastResponse.Headers.ETag.ShouldNotBeNull();

        var scope = await DeserializeAsync<ScopeResponse>(stream);
        scope.Id.ShouldBe(created.Id);
        scope.Dimension.ShouldBe("environment");
    }

    [Fact]
    public async Task GetScope_NonExistentId_Returns404()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = KiotaClientFactory.Create(factory);

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes[Guid.CreateVersion7()].GetAsync(cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListScopes_ReturnsPagedResults()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        await CreateScopeAsync(client, "environment", ["dev", "prod"]);
        await CreateScopeAsync(client, "region", ["us", "eu"]);

        // Act
        using var stream = await client.Api.Scopes.GetAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await DeserializeAsync<PaginatedResponse<ScopeResponse>>(stream);
        page.Data.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListScopes_WithPaginationParams_AppliesCorrectly()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        await CreateScopeAsync(client, "alpha", ["a"]);
        await CreateScopeAsync(client, "bravo", ["b"]);
        await CreateScopeAsync(client, "charlie", ["c"]);

        // Act
        using var stream = await client.Api.Scopes.GetAsync(config =>
        {
            config.QueryParameters.Limit = "2";
            config.QueryParameters.SortField = "dimension";
            config.QueryParameters.SortOrder = "asc";
        }, cancellationToken: TestCancellationToken);

        // Assert
        var page = await DeserializeAsync<PaginatedResponse<ScopeResponse>>(stream);
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
        var (client, handler) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);
        var etag = $"\"{created.Version}\"";

        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev", "staging", "prod"],
            Description = "Updated"
        };

        // Act
        using var stream = await client.Api.Scopes[created.Id].PutAsync(updateRequest, config =>
        {
            config.Headers.Add("If-Match", etag);
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await DeserializeAsync<ScopeResponse>(stream);
        updated.AllowedValues.ShouldBe(["dev", "staging", "prod"]);
        updated.Description.ShouldBe("Updated");
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateScope_WithStaleIfMatch_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);
        var staleEtag = "\"999\"";

        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev"],
        };

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes[created.Id].PutAsync(updateRequest, config =>
            {
                config.Headers.Add("If-Match", staleEtag);
            }, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateScope_WithoutIfMatch_Returns428()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);

        var updateRequest = new UpdateScopeRequest
        {
            Dimension = "environment",
            AllowedValues = ["dev"],
        };

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes[created.Id].PutAsync(updateRequest, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe(428);
    }

    [Fact]
    public async Task DeleteScope_WithValidIfMatch_Returns204()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);
        var etag = $"\"{created.Version}\"";

        // Act
        using var stream = await client.Api.Scopes[created.Id].DeleteAsync(config =>
        {
            config.Headers.Add("If-Match", etag);
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteScope_WithStaleIfMatch_Returns409()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, _) = KiotaClientFactory.Create(factory);
        var created = await CreateScopeAsync(client, "environment", ["dev", "prod"]);
        var staleEtag = "\"999\"";

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.Scopes[created.Id].DeleteAsync(config =>
            {
                config.Headers.Add("If-Match", staleEtag);
            }, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.Conflict);
    }

    private static async Task<ScopeResponse> CreateScopeAsync(GroundControlApiClient client, string dimension, List<string> allowedValues)
    {
        var request = new CreateScopeRequest
        {
            Dimension = dimension,
            AllowedValues = allowedValues
        };

        using var stream = await client.Api.Scopes.PostAsync(request, cancellationToken: TestCancellationToken);
        return await DeserializeAsync<ScopeResponse>(stream);
    }

    private static async Task<T> DeserializeAsync<T>(Stream? stream) where T : class
    {
        stream.ShouldNotBeNull();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions).ConfigureAwait(false);
        result.ShouldNotBeNull();

        return result;
    }
}