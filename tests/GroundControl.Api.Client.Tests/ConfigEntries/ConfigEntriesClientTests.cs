using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Client.Contracts;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Persistence.Contracts;
using CreateConfigEntryRequest = GroundControl.Api.Client.Contracts.CreateConfigEntryRequest;
using CreateTemplateRequest = GroundControl.Api.Features.Templates.Contracts.CreateTemplateRequest;
using ScopedValueRequest = GroundControl.Api.Client.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Client.Tests.ConfigEntries;

public sealed class ConfigEntriesClientTests : ApiHandlerTestBase
{
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
        var (client, handler) = ApiClientFactory.Create(factory);
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
        await client.CreateConfigEntryHandlerAsync(request, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = handler.DeserializeCapturedResponse<ConfigEntryResponse>();
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
        var (client, handler) = ApiClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, handler, "AppSettings:Feature", template.Id);

        // Act
        await client.GetConfigEntryHandlerAsync(created.Id, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);

        var entry = handler.DeserializeCapturedResponse<ConfigEntryResponse>();
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
        var (client, handler) = ApiClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        await CreateConfigEntryAsync(client, handler, "Zeta:Key", template.Id);
        await CreateConfigEntryAsync(client, handler, "Alpha:Key", template.Id);

        // Act
        await client.ListConfigEntriesHandlerAsync(
            ownerId: template.Id,
            ownerType: (int)ConfigEntryOwnerType.Template,
            sortField: "key",
            sortOrder: "asc",
            cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);

        var page = handler.DeserializeCapturedResponse<PaginatedResponse<ConfigEntryResponse>>();
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
        var (client, handler) = ApiClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, handler, "Database:Timeout", template.Id);

        var updateRequest = new GroundControl.Api.Features.ConfigEntries.Contracts.UpdateConfigEntryRequest
        {
            ValueType = "Integer",
            Values = [new GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest { Value = "30" }],
            IsSensitive = true,
            Description = "Connection timeout"
        };

        // Act
        httpClient.DefaultRequestHeaders.Add("If-Match", $"\"{created.Version}\"");
        var response = await httpClient.PutAsJsonAsync(
            $"/api/config-entries/{created.Id}", updateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
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
        var (client, _) = ApiClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");

        var request = new CreateConfigEntryRequest
        {
            OwnerId = template.Id,
            OwnerType = (int)ConfigEntryOwnerType.Template,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = "default" }]
        };

        // Act
        var exception = await Should.ThrowAsync<GroundControlApiClientException>(
            () => client.CreateConfigEntryHandlerAsync(request, TestCancellationToken));

        // Assert
        exception.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteConfigEntry_WithIfMatch_Returns204()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = ApiClientFactory.Create(factory);
        var template = await CreateTemplateViaHttpAsync(httpClient, "Test Template");
        var created = await CreateConfigEntryAsync(client, handler, "ToDelete:Key", template.Id);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/config-entries/{created.Id}");
        request.Headers.Add("If-Match", $"\"{created.Version}\"");

        // Act
        var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(
        GroundControlClient client,
        ResponseCapturingHandler handler,
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

        await client.CreateConfigEntryHandlerAsync(request, TestCancellationToken);
        return handler.DeserializeCapturedResponse<ConfigEntryResponse>();
    }

    private static async Task<TemplateResponse> CreateTemplateViaHttpAsync(HttpClient httpClient, string name)
    {
        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = $"{name} template"
        };

        var response = await httpClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        template.ShouldNotBeNull();

        return template;
    }
}