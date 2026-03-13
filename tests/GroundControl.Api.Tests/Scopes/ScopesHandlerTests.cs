using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Tests.Infrastructure;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Scopes;

[Collection("MongoDB")]
public sealed class ScopesHandlerTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MongoFixture _mongoFixture;

    public ScopesHandlerTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task DeleteScope_WhenScopeValueIsReferenced_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var timestamp = DateTimeOffset.UtcNow;

        await clientCollection.InsertOneAsync(new Client
        {
            Id = Guid.CreateVersion7(),
            ProjectId = Guid.CreateVersion7(),
            Scopes = new Dictionary<string, string> { ["environment"] = "prod" },
            Secret = "secret",
            Name = "test-client",
            IsActive = true,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        }, cancellationToken: cancellationToken);

        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes/{createdScope.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/scopes/{createdScope.Id}"));
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
        detail.ShouldContain("prod");
    }

    [Fact]
    public async Task DeleteScope_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes/{createdScope.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/scopes/{createdScope.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var missingResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes/{createdScope.Id}"), cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteScope_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/scopes/{createdScope.Id}"));
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
    public async Task GetScope_WithExistingId_ReturnsScopeAndEntityTag()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/scopes/{createdScope.Id}"), cancellationToken);
        var scope = await ReadScopeAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        scope.Id.ShouldBe(createdScope.Id);
    }

    [Fact]
    public async Task GetScope_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/scopes/{Guid.CreateVersion7()}"), cancellationToken);
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
    public async Task GetScopes_WithForwardAndBackwardCursorPagination_ReturnsFlattenedPaginatedResponse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await CreateScopeAsync(apiClient, "gamma", ["g1"], cancellationToken);
        await CreateScopeAsync(apiClient, "alpha", ["a1"], cancellationToken);
        await CreateScopeAsync(apiClient, "beta", ["b1"], cancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync(RelativeUri("/api/scopes?limit=2&sortField=dimension&sortOrder=asc"), cancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, cancellationToken);

        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor!);
        var secondResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes?limit=2&sortField=dimension&sortOrder=asc&after={nextCursor}"), cancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, cancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes?limit=2&sortField=dimension&sortOrder=asc&before={previousCursor}"), cancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, cancellationToken);

        // Assert
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstPage.Data.Select(scope => scope.Dimension).ShouldBe(["alpha", "beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondPage.Data.Select(scope => scope.Dimension).ShouldBe(["gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        previousPage.Data.Select(scope => scope.Dimension).ShouldBe(["alpha", "beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task PostScope_WithDuplicateDimension_ReturnsValidationProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync(RelativeUri("/api/scopes"), CreateRequest("environment", ["dev", "prod"]), WebJsonSerializerOptions, cancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/scopes"), CreateRequest("Environment", ["qa"]), WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadValidationProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Dimension");
        problem.Errors["Dimension"].ShouldContain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task PostScope_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("environment", ["dev", "prod"]);

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/scopes"), request, WebJsonSerializerOptions, cancellationToken);
        var scope = await ReadScopeAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        scope.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/scopes/{scope.Id}");
        scope.Dimension.ShouldBe("environment");
        scope.AllowedValues.ShouldBe(["dev", "prod"]);
    }

    [Fact]
    public async Task PutScope_WithCorrectIfMatch_ReturnsUpdatedScope()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/scopes/{createdScope.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/scopes/{createdScope.Id}"));
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var scope = await ReadScopeAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        scope.Version.ShouldBe(2);
        scope.AllowedValues.ShouldBe(["stage", "prod"]);
    }

    [Fact]
    public async Task PutScope_WithoutIfMatch_ReturnsPreconditionRequiredProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/scopes/{createdScope.Id}"));
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);

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
    public async Task PutScope_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/scopes/{createdScope.Id}"));
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);
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

    private static CreateScopeRequest CreateRequest(string dimension, IReadOnlyList<string> allowedValues) => new()
    {
        Dimension = dimension,
        AllowedValues = [.. allowedValues],
        Description = $"{dimension} scope"
    };

    private static async Task<ScopeResponse> CreateScopeAsync(HttpClient apiClient, string dimension, IReadOnlyList<string> allowedValues, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/scopes"),
                CreateRequest(dimension, allowedValues),
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadScopeAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaginatedResponse<ScopeResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ScopeResponse>>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<HttpValidationProblemDetails?> ReadValidationProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<ScopeResponse> ReadScopeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        scope.ShouldNotBeNull();

        return scope;
    }

    private static Uri RelativeUri(string relativePath) => new(relativePath, UriKind.Relative);
}