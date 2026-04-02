using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using Microsoft.Kiota.Abstractions;
using CreateConfigEntryRequest = GroundControl.Api.Client.Models.CreateConfigEntryRequest;
using ScopedValueRequest = GroundControl.Api.Client.Models.ScopedValueRequest;
using UpdateConfigEntryRequest = GroundControl.Api.Client.Models.UpdateConfigEntryRequest;

namespace GroundControl.Api.Client.Tests.ConfigEntries;

public sealed class ConfigEntriesClientTests : ApiHandlerTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ConfigEntriesClientTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateConfigEntry_WithScopedValues_ReturnsCreatedEntry()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");

        var request = new CreateConfigEntryRequest
        {
            Key = "Logging:LogLevel:Default",
            OwnerId = template.Id,
            OwnerType = (int)ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values =
            [
                new ScopedValueRequest { Value = "Information" },
                new ScopedValueRequest { Value = "Debug" }
            ],
            Description = "Default log level"
        };

        // Act
        using var stream = await client.Api.ConfigEntries.PostAsync(request, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = await DeserializeAsync<ConfigEntryResponse>(stream);
        entry.Key.ShouldBe("Logging:LogLevel:Default");
        entry.OwnerId.ShouldBe(template.Id);
        entry.OwnerType.ShouldBe(ConfigEntryOwnerType.Template);
        entry.ValueType.ShouldBe("String");
        entry.Values.Count.ShouldBe(2);
        entry.Description.ShouldBe("Default log level");
        entry.Version.ShouldBe(1);
    }

    [Fact]
    public async Task GetConfigEntry_ReturnsEntryWithValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, "AppSettings:Feature", template.Id);

        // Act
        using var stream = await client.Api.ConfigEntries[created.Id].GetAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entry = await DeserializeAsync<ConfigEntryResponse>(stream);
        entry.Id.ShouldBe(created.Id);
        entry.Key.ShouldBe("AppSettings:Feature");
        entry.Values.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListConfigEntries_WithSortParams_AppliesSorting()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        await CreateConfigEntryAsync(client, "Zeta:Key", template.Id);
        await CreateConfigEntryAsync(client, "Alpha:Key", template.Id);

        // Act
        using var stream = await client.Api.ConfigEntries.GetAsync(config =>
        {
            config.QueryParameters.OwnerId = template.Id;
            config.QueryParameters.OwnerType = (int)ConfigEntryOwnerType.Template;
            config.QueryParameters.SortField = "key";
            config.QueryParameters.SortOrder = "asc";
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await DeserializeAsync<PaginatedResponse<ConfigEntryResponse>>(stream);
        page.Data.Count.ShouldBe(2);
        page.Data[0].Key.ShouldBe("Alpha:Key");
        page.Data[1].Key.ShouldBe("Zeta:Key");
    }

    [Fact]
    public async Task UpdateConfigEntry_ModifiesValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, "Database:Timeout", template.Id);
        var etag = $"\"{created.Version}\"";

        var updateRequest = new UpdateConfigEntryRequest
        {
            ValueType = "Integer",
            Values = [new ScopedValueRequest { Value = "30" }],
            IsSensitive = true,
            Description = "Connection timeout"
        };

        // Act
        using var stream = await client.Api.ConfigEntries[created.Id].PutAsync(updateRequest, config =>
        {
            config.Headers.Add("If-Match", etag);
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await DeserializeAsync<ConfigEntryResponse>(stream);
        updated.ValueType.ShouldBe("Integer");
        updated.IsSensitive.ShouldBeTrue();
        updated.Description.ShouldBe("Connection timeout");
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task CreateConfigEntry_MissingKey_Returns400()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, _) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");

        var request = new CreateConfigEntryRequest
        {
            OwnerId = template.Id,
            OwnerType = (int)ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "default" }]
        };

        // Act
        var exception = await Should.ThrowAsync<ApiException>(
            () => client.Api.ConfigEntries.PostAsync(request, cancellationToken: TestCancellationToken));

        // Assert
        exception.ResponseStatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteConfigEntry_WithIfMatch_Returns204()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, "ToDelete:Key", template.Id);
        var etag = $"\"{created.Version}\"";

        // Act
        using var stream = await client.Api.ConfigEntries[created.Id].DeleteAsync(config =>
        {
            config.Headers.Add("If-Match", etag);
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        GroundControlApiClient client,
        string key,
        Guid ownerId)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = (int)ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "default" }]
        };

        using var stream = await client.Api.ConfigEntries.PostAsync(request, cancellationToken: TestCancellationToken);
        return await DeserializeAsync<ConfigEntryResponse>(stream);
    }

    private static async Task<TemplateResponse> CreateTemplateViaHttpAsync(HttpClient httpClient, string name)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template"
        };

        var response = await httpClient.PostAsJsonAsync("/api/templates", request, JsonOptions, TestCancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(JsonOptions, TestCancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }

    private static async Task<T> DeserializeAsync<T>(Stream? stream) where T : class
    {
        stream.ShouldNotBeNull();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions).ConfigureAwait(false);
        result.ShouldNotBeNull();

        return result;
    }
}