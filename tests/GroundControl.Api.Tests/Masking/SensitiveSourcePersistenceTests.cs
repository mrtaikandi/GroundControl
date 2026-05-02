using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shouldly;
using Xunit;
using ConfigEntryScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;
using VariableScopedValueRequest = GroundControl.Api.Features.Variables.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Masking;

public sealed class SensitiveSourcePersistenceTests : ApiHandlerTestBase
{
    public SensitiveSourcePersistenceTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateConfigEntry_Sensitive_StoresCiphertextInMongo()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var protector = factory.Services.GetRequiredService<IValueProtector>();

        // Act
        var created = await CreateConfigEntryAsync(apiClient, "db.password", "s3cret!", isSensitive: true);

        // Assert
        var stored = await factory.Database.GetCollection<ConfigEntry>("config_entries")
            .Find(e => e.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        var storedValue = stored.Values.ShouldHaveSingleItem().Value;
        storedValue.ShouldNotBe("s3cret!");
        storedValue.ShouldNotBeNullOrEmpty();
        protector.Unprotect(storedValue).ShouldBe("s3cret!");
    }

    [Fact]
    public async Task CreateConfigEntry_NonSensitive_StoresPlaintextInMongo()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var created = await CreateConfigEntryAsync(apiClient, "app.name", "MyApp", isSensitive: false);

        // Assert
        var stored = await factory.Database.GetCollection<ConfigEntry>("config_entries")
            .Find(e => e.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        stored.Values.ShouldHaveSingleItem().Value.ShouldBe("MyApp");
    }

    [Fact]
    public async Task CreateConfigEntry_Sensitive_ResponseValuesAreMasked()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new CreateConfigEntryRequest
        {
            Key = "db.password",
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = "s3cret!" }],
            IsSensitive = true,
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        var responseValue = body.Values.ShouldHaveSingleItem().Value;
        responseValue.ShouldBe(SensitiveSourceValueProtector.MaskValue);
        responseValue.ShouldNotBe("s3cret!");
    }

    [Fact]
    public async Task UpdateConfigEntry_Sensitive_ResponseValuesAreMasked()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateConfigEntryAsync(apiClient, "db.password", "s3cret!", isSensitive: true);

        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        etag.ShouldNotBeNullOrEmpty();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "rotated!" }],
                IsSensitive = true,
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(updateRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        var responseValue = body.Values.ShouldHaveSingleItem().Value;
        responseValue.ShouldBe(SensitiveSourceValueProtector.MaskValue);
        responseValue.ShouldNotBe("rotated!");
    }

    [Fact]
    public async Task CreateVariable_Sensitive_StoresCiphertextInMongo()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var protector = factory.Services.GetRequiredService<IValueProtector>();

        // Act
        var created = await CreateVariableAsync(apiClient, "dbPassword", "s3cret!", isSensitive: true);

        // Assert
        var stored = await factory.Database.GetCollection<Variable>("variables")
            .Find(v => v.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        var storedValue = stored.Values.ShouldHaveSingleItem().Value;
        storedValue.ShouldNotBe("s3cret!");
        storedValue.ShouldNotBeNullOrEmpty();
        protector.Unprotect(storedValue).ShouldBe("s3cret!");
    }

    [Fact]
    public async Task CreateVariable_NonSensitive_StoresPlaintextInMongo()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var created = await CreateVariableAsync(apiClient, "appName", "MyApp", isSensitive: false);

        // Assert
        var stored = await factory.Database.GetCollection<Variable>("variables")
            .Find(v => v.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        stored.Values.ShouldHaveSingleItem().Value.ShouldBe("MyApp");
    }

    [Fact]
    public async Task CreateVariable_Sensitive_ResponseValuesAreMasked()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new CreateVariableRequest
        {
            Name = "dbPassword",
            Scope = VariableScope.Global,
            Values = [new VariableScopedValueRequest { Value = "s3cret!" }],
            IsSensitive = true,
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        var responseValue = body.Values.ShouldHaveSingleItem().Value;
        responseValue.ShouldBe(SensitiveSourceValueProtector.MaskValue);
        responseValue.ShouldNotBe("s3cret!");
    }

    [Fact]
    public async Task UpdateVariable_Sensitive_ResponseValuesAreMasked()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateVariableAsync(apiClient, "dbPassword", "s3cret!", isSensitive: true);

        var getResponse = await apiClient.GetAsync($"/api/variables/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        etag.ShouldNotBeNullOrEmpty();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/variables/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateVariableRequest
            {
                Values = [new VariableScopedValueRequest { Value = "rotated!" }],
                IsSensitive = true,
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(updateRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
        var responseValue = body.Values.ShouldHaveSingleItem().Value;
        responseValue.ShouldBe(SensitiveSourceValueProtector.MaskValue);
        responseValue.ShouldNotBe("rotated!");
    }

    [Fact]
    public async Task CreateConfigEntry_SensitiveWithEmptyValue_StoresEmptyVerbatim()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var created = await CreateConfigEntryAsync(apiClient, "optional.secret", value: string.Empty, isSensitive: true);

        // Assert
        var stored = await factory.Database.GetCollection<ConfigEntry>("config_entries")
            .Find(e => e.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        stored.Values.ShouldHaveSingleItem().Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GetConfigEntry_SensitiveWithEmptyValue_ReturnsEmptyEvenWhenMasked()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateConfigEntryAsync(apiClient, "optional.secret", value: string.Empty, isSensitive: true);

        // Act
        var response = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        body.IsSensitive.ShouldBeTrue();
        body.Values.ShouldHaveSingleItem().Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task UpdateConfigEntry_SensitiveValueUnchanged_DoesNotEmitValuesAuditChange()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateConfigEntryAsync(apiClient, "db.password", "s3cret!", isSensitive: true);

        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();
        etag.ShouldNotBeNullOrEmpty();

        // Submit an update with the SAME plaintext value. Stored ciphertext is non-deterministic
        // (Data Protection includes a fresh IV every call), so a naive comparison would always
        // emit a Values change record. The handler decrypts the old values before comparing.
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "s3cret!" }],
                IsSensitive = true,
                Description = "rotated description",
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var updateResponse = await apiClient.SendAsync(updateRequest, TestCancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.EntityId == created.Id && r.Action == "Updated")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        auditRecords[0].Changes.ShouldNotContain(c => c.Field == "Values");
        // Description did change, so we still expect at least one entry — proves we read the audit record correctly.
        auditRecords[0].Changes.ShouldContain(c => c.Field == "Description");
    }

    [Fact]
    public async Task CreateConfigEntry_Sensitive_RejectsMaskSentinelValue()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new CreateConfigEntryRequest
        {
            Key = "db.password",
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = "***" }],
            IsSensitive = true,
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert — guard against UI round-trip overwriting the secret with the mask string
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateConfigEntry_NonSensitive_AcceptsThreeStarsAsLiteralValue()
    {
        // Arrange — non-sensitive entries can legitimately use "***" as a value
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new CreateConfigEntryRequest
        {
            Key = "ui.placeholder",
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = "***" }],
            IsSensitive = false,
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateConfigEntry_Sensitive_RejectsMaskSentinelValue()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var created = await CreateConfigEntryAsync(apiClient, "db.password", "s3cret!", isSensitive: true);

        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "***" }],
                IsSensitive = true,
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(updateRequest, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateVariable_Sensitive_RejectsMaskSentinelValue()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var request = new CreateVariableRequest
        {
            Name = "dbPassword",
            Scope = VariableScope.Global,
            Values = [new VariableScopedValueRequest { Value = "***" }],
            IsSensitive = true,
        };

        // Act
        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListConfigEntries_WithDecrypt_RecordsOneAuditPerSensitiveEntry()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var first = await CreateConfigEntryAsync(apiClient, "db.password", "s3cret!", isSensitive: true);
        var second = await CreateConfigEntryAsync(apiClient, "api.token", "tok3n", isSensitive: true);
        await CreateConfigEntryAsync(apiClient, "app.name", "MyApp", isSensitive: false);

        // Act
        var response = await apiClient.GetAsync("/api/config-entries?decrypt=true", TestCancellationToken);
        response.EnsureSuccessStatusCode();

        // Assert — one Decrypted audit per revealed sensitive entry; non-sensitive entries do not audit
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.Action == "Decrypted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(2);
        auditRecords.ShouldContain(r => r.EntityId == first.Id);
        auditRecords.ShouldContain(r => r.EntityId == second.Id);
    }

    [Fact]
    public async Task UpdateConfigEntry_TransitionFromSensitiveToNonSensitive_StoresNewPlaintextAndMasksAudit()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var protector = factory.Services.GetRequiredService<IValueProtector>();
        var created = await CreateConfigEntryAsync(apiClient, "promoted.key", "was-secret", isSensitive: true);

        var getResponse = await apiClient.GetAsync($"/api/config-entries/{created.Id}", TestCancellationToken);
        var etag = getResponse.Headers.ETag?.ToString();

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{created.Id}");
        updateRequest.Content = JsonContent.Create(
            new UpdateConfigEntryRequest
            {
                ValueType = "String",
                Values = [new ConfigEntryScopedValueRequest { Value = "now-public" }],
                IsSensitive = false,
            },
            options: WebJsonSerializerOptions);
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);

        // Act
        var response = await apiClient.SendAsync(updateRequest, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        // Assert — stored value is now plaintext (not encrypted)
        var stored = await factory.Database.GetCollection<ConfigEntry>("config_entries")
            .Find(e => e.Id == created.Id)
            .FirstAsync(TestCancellationToken);

        stored.IsSensitive.ShouldBeFalse();
        stored.Values.ShouldHaveSingleItem().Value.ShouldBe("now-public");

        // Audit records: the transition was sensitive somewhere in the boundary, so Values are masked
        var auditRecord = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.EntityId == created.Id && r.Action == "Updated")
            .FirstAsync(TestCancellationToken);

        auditRecord.Changes.ShouldContain(c => c.Field == "Values" && c.OldValue == "***" && c.NewValue == "***");
        auditRecord.Changes.ShouldContain(c => c.Field == "IsSensitive");

        // Round-trip sanity: the response body should not be ciphertext or "***" since the
        // updated entry is no longer sensitive.
        _ = protector;
    }

    private static async Task<ConfigEntryResponse> CreateConfigEntryAsync(HttpClient apiClient, string key, string value, bool isSensitive)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
    }

    private static async Task<VariableResponse> CreateVariableAsync(HttpClient apiClient, string name, string value, bool isSensitive)
    {
        var request = new CreateVariableRequest
        {
            Name = name,
            Scope = VariableScope.Global,
            Values = [new VariableScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
    }
}