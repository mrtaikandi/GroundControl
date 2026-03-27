using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Scopes.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.ConfigEntries;

public sealed class ConfigEntriesHandlerTests : ApiHandlerTestBase
{
    public ConfigEntriesHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task PostConfigEntry_WithValidBody_ReturnsCreatedResponseWithLocationHeader()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "Logging:LogLevel:Default",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "Information" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var entry = await ReadConfigEntryAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "InvalidType",
            Values = [new ScopedValueRequest { Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("ValueType");
        problem.Errors["ValueType"].ShouldContain(e => e.Contains("not supported"));
    }

    [Fact]
    public async Task PostConfigEntry_WithNonBooleanValueForBooleanType_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "Feature:Enabled",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "Boolean",
            Values = [new ScopedValueRequest { Value = "notABool" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Values");
        problem.Errors["Values"].ShouldContain(e => e.Contains("not a valid Boolean"));
    }

    [Fact]
    public async Task PostConfigEntry_WithInvalidScopeDimension_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["nonexistent"] = "value" }, Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Values");
        problem.Errors["Values"].ShouldContain(e => e.Contains("does not exist"));
    }

    [Fact]
    public async Task PostConfigEntry_WithInvalidScopeValue_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "TestKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Scopes = new Dictionary<string, string> { ["environment"] = "invalid" }, Value = "test" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var problem = await response.ReadValidationProblemAsync(TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Values");
        problem.Errors["Values"].ShouldContain(e => e.Contains("not allowed"));
    }

    [Fact]
    public async Task PostConfigEntry_WithValidScopedValues_ReturnsCreated()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        await CreateScopeAsync(apiClient, "environment", ["dev", "prod"], TestCancellationToken);
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
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        var entry = await ReadConfigEntryAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        entry.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PostConfigEntry_DuplicateKeyWithinOwner_ReturnsConflict()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var request = new CreateConfigEntryRequest
        {
            Key = "DuplicateKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "first" }],
        };

        await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);

        var duplicateRequest = new CreateConfigEntryRequest
        {
            Key = "DuplicateKey",
            OwnerId = template.Id,
            OwnerType = ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "second" }],
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", duplicateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetConfigEntry_WithExistingId_ReturnsEntryAndEntityTag()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var entry = await ReadConfigEntryAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync($"/api/config-entries/{Guid.CreateVersion7()}", TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template1 = await CreateTemplateAsync(apiClient, "Template One", TestCancellationToken);
        var template2 = await CreateTemplateAsync(apiClient, "Template Two", TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key1", template1.Id, TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key2", template1.Id, TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "Key3", template2.Id, TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/config-entries?limit=25&sortField=key&sortOrder=asc&ownerId={template1.Id}&ownerType=Template", TestCancellationToken);

        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(e => e.OwnerId == template1.Id);
    }

    [Fact]
    public async Task GetConfigEntries_WithKeyPrefixFilter_ReturnsFilteredResults()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "AppSettings:Feature1", template.Id, TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "AppSettings:Feature2", template.Id, TestCancellationToken);
        await CreateConfigEntryAsync(apiClient, "Logging:Level", template.Id, TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/config-entries?limit=25&sortField=key&sortOrder=asc&ownerId={template.Id}&ownerType=Template&keyPrefix=AppSettings", TestCancellationToken);

        var page = await ReadPageAsync(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Data.Count.ShouldBe(2);
        page.Data.ShouldAllBe(e => e.Key.StartsWith("AppSettings"));
    }

    [Fact]
    public async Task PutConfigEntry_WithCorrectIfMatch_ReturnsUpdatedEntry()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
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
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var entry = await ReadConfigEntryAsync(response, TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, TestCancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        request.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ScopedValueRequest { Value = "updated" }],
            },
            options: WebJsonSerializerOptions);

        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var problem = await response.ReadProblemAsync(TestCancellationToken);

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
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Test Template", TestCancellationToken);
        var created = await CreateConfigEntryAsync(apiClient, "TestKey", template.Id, TestCancellationToken);
        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/config-entries/{created.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(request, TestCancellationToken);
        var missingResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTemplate_CascadesDeleteToConfigEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var template = await CreateTemplateAsync(apiClient, "Cascade Template", TestCancellationToken);
        var entry = await CreateConfigEntryAsync(apiClient, "CascadeKey", template.Id, TestCancellationToken);

        var getTemplateResponse = await apiClient.GetAsync($"/api/templates/{template.Id}", TestCancellationToken);
        var etag = getTemplateResponse.Headers.ETag?.ToString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/templates/{template.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var deleteResponse = await apiClient.SendAsync(deleteRequest, TestCancellationToken);
        var entryResponse = await apiClient.GetAsync($"/api/config-entries/{entry.Id}", TestCancellationToken);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId, CancellationToken cancellationToken,
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
                "/api/config-entries",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await ReadConfigEntryAsync(response, TestCancellationToken).ConfigureAwait(false);
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template",
        };

        var response = await apiClient.PostAsJsonAsync(
                "/api/templates",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
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
                "/api/scopes",
                request,
                WebJsonSerializerOptions, TestCancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var scope = await response.Content.ReadFromJsonAsync<ScopeResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        scope.ShouldNotBeNull();

        return scope;
    }

    private static async Task<PaginatedResponse<ConfigEntryResponse>> ReadPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ConfigEntryResponse>>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        page.ShouldNotBeNull();

        return page;
    }

    private static async Task<ConfigEntryResponse> ReadConfigEntryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var entry = await response.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        entry.ShouldNotBeNull();

        return entry;
    }
}