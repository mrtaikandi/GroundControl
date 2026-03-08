using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Tests.Infrastructure;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.ConfigEntries;

[Collection("MongoDB")]
public sealed class ConfigEntriesHandlerTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MongoFixture _mongoFixture;

    public ConfigEntriesHandlerTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task PostConfigEntry_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "Logging:LogLevel:Default",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "Information" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var entry = await ReadConfigEntryAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        entry.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ToString().ShouldBe($"/api/config-entries/{entry.Id}");
        entry.Key.ShouldBe("Logging:LogLevel:Default");
        entry.OwnerId.ShouldBe(template.Id);
        entry.OwnerType.ShouldBe(ConfigEntryOwnerType.Template);
        entry.ValueType.ShouldBe("String");
        entry.Values.ShouldHaveSingleItem();
        entry.IsSensitive.ShouldBeFalse();
    }

    [Fact]
    public async Task PostConfigEntry_WithInvalidValueType_ReturnsBadRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "InvalidType",
            Values = [new ScopedValueRequest { Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("not supported");
    }

    [Fact]
    public async Task PostConfigEntry_WithNonBooleanValueForBooleanType_ReturnsBadRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "Feature:Enabled",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "Boolean",
            Values = [new ScopedValueRequest { Value = "notABool" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("not a valid Boolean");
    }

    [Fact]
    public async Task PostConfigEntry_WithInvalidScopeDimension_ReturnsBadRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["nonexistent"] = "value" }, Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("does not exist");
    }

    [Fact]
    public async Task PostConfigEntry_WithInvalidScopeValue_ReturnsBadRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["environment"] = "invalid" }, Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("not allowed");
    }

    [Fact]
    public async Task PostConfigEntry_WithValidScopedValues_ReturnsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "ConnectionString",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values =
            [
                new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["environment"] = "dev" }, Value = "Server=dev-db" },
                new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["environment"] = "prod" }, Value = "Server=prod-db" },
            ],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);
        var entry = await ReadConfigEntryAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        entry.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PostConfigEntry_DuplicateKeyWithinOwner_ReturnsConflict()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "DuplicateKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "first" }],
        };

        await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), request, WebJsonSerializerOptions, cancellationToken);

        var duplicateRequest = new CreateConfigEntryRequest
        {
            Key = "DuplicateKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "second" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync(RelativeUri("/api/config-entries"), duplicateRequest, WebJsonSerializerOptions, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetConfigEntry_WithExistingId_ReturnsEntryAndEntityTag()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, cancellationToken);

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{created.Id}"), cancellationToken);
        var entry = await ReadConfigEntryAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"1\"");
        entry.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetConfigEntry_WithUnknownId_ReturnsNotFound()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{Guid.CreateVersion7()}"), cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetConfigEntries_WithOwnerFilter_ReturnsFilteredResults()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template1 = await CreateTemplateAsync(apiClient, "Template One", cancellationToken);
        var template2 = await CreateTemplateAsync(apiClient, "Template Two", cancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key1", template1.Id, cancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key2", template1.Id, cancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key3", template2.Id, cancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            RelativeUri($"/api/config-entries?limit=25&sortField=key&sortOrder=asc&ownerId={template1.Id}&ownerType=Template"),
            cancellationToken);

        var page = await ReadPageAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(e => e.OwnerId == template1.Id);
    }

    [Fact]
    public async Task GetConfigEntries_WithKeyPrefixFilter_ReturnsFilteredResults()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        await CreateConfigEntryAsync(apiClient, "AppSettings:Feature1", template.Id, cancellationToken);
        await CreateConfigEntryAsync(apiClient, "AppSettings:Feature2", template.Id, cancellationToken);
        await CreateConfigEntryAsync(apiClient, "Logging:Level", template.Id, cancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            RelativeUri($"/api/config-entries?limit=25&sortField=key&sortOrder=asc&ownerId={template.Id}&ownerType=Template&keyPrefix=AppSettings"),
            cancellationToken);

        var page = await ReadPageAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(e => e.Key.StartsWith("AppSettings"));
    }

    [Fact]
    public async Task PutConfigEntry_WithCorrectIfMatch_ReturnsUpdatedEntry()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{created.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/config-entries/{created.Id}"));
        request.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "Integer",
                Values = [new ScopedValueRequest { Value = "42" }],
                IsSensitive = true,
                Description = "Updated description",
            },
            options: WebJsonSerializerOptions);

        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var entry = await ReadConfigEntryAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.ToString().ShouldBe("\"2\"");
        entry.Version.ShouldBe(2);
        entry.ValueType.ShouldBe("Integer");
        entry.IsSensitive.ShouldBeTrue();
        entry.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task PutConfigEntry_WithStaleIfMatch_ReturnsConflict()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, RelativeUri($"/api/config-entries/{created.Id}"));
        request.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ScopedValueRequest { Value = "updated" }],
            },
            options: WebJsonSerializerOptions);

        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var problem = await ReadProblemAsync(response, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("Version conflict");
    }

    [Fact]
    public async Task DeleteConfigEntry_WithCorrectIfMatch_ReturnsNoContent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", cancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, cancellationToken);
        var getResponse = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{created.Id}"), cancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/config-entries/{created.Id}"));
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, cancellationToken);
        var missingResponse = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{created.Id}"), cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTemplate_CascadesDeleteToConfigEntries()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new GroundControlApiFactory(_mongoFixture);
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Cascade Template", cancellationToken);
        var entry = await CreateConfigEntryAsync(apiClient, "CascadeKey", template.Id, cancellationToken);

        var getTemplateResponse = await apiClient.GetAsync(RelativeUri($"/api/templates/{template.Id}"), cancellationToken);
        var etag = getTemplateResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, RelativeUri($"/api/templates/{template.Id}"));
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, cancellationToken);
        var entryResponse = await apiClient.GetAsync(RelativeUri($"/api/config-entries/{entry.Id}"), cancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        CancellationToken cancellationToken,
        string valueType = "String",
        string value = "default")
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = valueType,
            Values = [new ScopedValueRequest { Value = value }],
        };

        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/config-entries"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadConfigEntryAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template",
        };

        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/templates"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }

    private static async Task<ScopeResponse> CreateScopeAsync(HttpClient apiClient, string dimension, List<string> allowedValues, CancellationToken cancellationToken)
    {
        var request = new CreateScopeRequest
        {
            Dimension = dimension,
            AllowedValues = allowedValues,
        };

        var response = await apiClient.PostAsJsonAsync(
                RelativeUri("/api/scopes"),
                request,
                WebJsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        scope.ShouldNotBeNull();

        return scope;
    }

    private static async Task<PaginatedResponse<ConfigEntryResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ConfigEntryResponse>>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ProblemDetails>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);

    private static async Task<ConfigEntryResponse> ReadConfigEntryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var entry = await response.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        entry.ShouldNotBeNull();

        return entry;
    }

    private static Uri RelativeUri(string relativePath) => new(relativePath, UriKind.Relative);
}