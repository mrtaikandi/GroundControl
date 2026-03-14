using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Scopes;

[Collection("MongoDB")]
public sealed class ScopesHandlerTests : ApiHandlerTestBase
{
    public ScopesHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task DeleteScope_WhenScopeValueIsReferenced_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);
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
        }, cancellationToken: TestCancellationToken);

        var getResponse = await apiClient.GetAsync($"/api/scopes/{createdScope.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{createdScope.Id}");
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
        detail.ShouldContain("prod");
    }

    [Fact]
    public async Task DeleteScope_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/scopes/{createdScope.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{createdScope.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/scopes/{createdScope.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteScope_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{createdScope.Id}");
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
    public async Task GetScope_WithExistingId_ReturnsScopeAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/scopes/{createdScope.Id}", TestCancellationToken);
        var scope = await ReadScopeAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/scopes/{Guid.CreateVersion7()}", TestCancellationToken);
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
    public async Task GetScopes_WithForwardAndBackwardCursorPagination_ReturnsFlattenedPaginatedResponse()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateScopeAsync(apiClient, "gamma", ["g1"], TestCancellationToken);
        await CreateScopeAsync(apiClient, "alpha", ["a1"], TestCancellationToken);
        await CreateScopeAsync(apiClient, "beta", ["b1"], TestCancellationToken);

        // Act
        var firstResponse = await apiClient.GetAsync("/api/scopes?limit=2&sortField=dimension&sortOrder=asc", TestCancellationToken);
        var firstPage = await ReadPageAsync(firstResponse, TestCancellationToken);

        var nextCursor = Uri.EscapeDataString(firstPage.NextCursor!);
        var secondResponse = await apiClient.GetAsync($"/api/scopes?limit=2&sortField=dimension&sortOrder=asc&after={nextCursor}", TestCancellationToken);
        var secondPage = await ReadPageAsync(secondResponse, TestCancellationToken);

        var previousCursor = Uri.EscapeDataString(secondPage.PreviousCursor!);
        var previousResponse = await apiClient.GetAsync($"/api/scopes?limit=2&sortField=dimension&sortOrder=asc&before={previousCursor}", TestCancellationToken);
        var previousPage = await ReadPageAsync(previousResponse, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await apiClient.PostAsJsonAsync("/api/scopes", CreateRequest("environment", ["dev", "prod"]), WebJsonSerializerOptions, TestCancellationToken);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/scopes", CreateRequest("Environment", ["qa"]), WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var request = CreateRequest("environment", ["dev", "prod"]);

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/scopes", request, WebJsonSerializerOptions, TestCancellationToken);
        var scope = await ReadScopeAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/scopes/{createdScope.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{createdScope.Id}");
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var scope = await ReadScopeAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{createdScope.Id}");
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);

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
    public async Task PutScope_WithStaleIfMatch_ReturnsConflictProblemDetails()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var createdScope = await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/scopes/{createdScope.Id}");
        request.Content = JsonContent.Create(CreateRequest("environment", ["stage", "prod"]), options: WebJsonSerializerOptions);
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

    private static CreateScopeRequest CreateRequest(string dimension, IReadOnlyList<string> allowedValues) => new()
    {
        Dimension = dimension,
        AllowedValues = [.. allowedValues],
        Description = $"{dimension} scope"
    };

    private static async Task<ScopeResponse> CreateScopeAsync(HttpClient apiClient, string dimension, IReadOnlyList<string> allowedValues, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
                "/api/scopes",
                CreateRequest(dimension, allowedValues),
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadScopeAsync(response, TestCancellationToken).ConfigureAwait(false);
    }

    private static async Task<PaginatedResponse<ScopeResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ScopeResponse>>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ScopeResponse> ReadScopeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        scope.ShouldNotBeNull();

        return scope;
    }
}