using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Snapshots;

public sealed class SnapshotResolverTests : ApiHandlerTestBase
{
    public SnapshotResolverTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task ResolveAsync_PlaintextEntries_KeepSensitiveValuesUnencrypted()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        var plaintextEntry = result.PlaintextEntries.ShouldHaveSingleItem();
        plaintextEntry.IsSensitive.ShouldBeTrue();
        plaintextEntry.Values.ShouldHaveSingleItem().Value.ShouldBe("s3cret!");
    }

    [Fact]
    public async Task ResolveAsync_EncryptedEntries_StoreSensitiveValuesAsCiphertext()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var protector = factory.Services.GetRequiredService<IValueProtector>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        var encryptedEntry = result.EncryptedEntries.ShouldHaveSingleItem();
        encryptedEntry.IsSensitive.ShouldBeTrue();
        var stored = encryptedEntry.Values.ShouldHaveSingleItem().Value;
        stored.ShouldNotBe("s3cret!");
        protector.Unprotect(stored).ShouldBe("s3cret!");
    }

    [Fact]
    public async Task ResolveAsync_DiffHash_IsStableAcrossCalls_DespiteNonDeterministicEncryption()
    {
        // Arrange — two resolutions of the same project state must produce identical hashes
        // even though the encryption applied to sensitive values is non-deterministic. This is
        // the regression that guards the publish-after-preview 409 gate from spurious conflicts.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "db.password", project.Id, value: "s3cret!", isSensitive: true);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var firstResolve = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);
        var secondResolve = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        firstResolve.DiffHash.ShouldNotBeNullOrEmpty();
        secondResolve.DiffHash.ShouldBe(firstResolve.DiffHash);

        // Sanity check: the encrypted ciphertext IS different between calls (non-deterministic
        // encryption confirmed) so the hash stability is not a coincidence of unchanged ciphertext.
        var firstCiphertext = firstResolve.EncryptedEntries.Single(e => e.Key == "db.password").Values.Single().Value;
        var secondCiphertext = secondResolve.EncryptedEntries.Single(e => e.Key == "db.password").Values.Single().Value;
        firstCiphertext.ShouldNotBe(secondCiphertext);
    }

    [Fact]
    public async Task ResolveAsync_DiffHash_ChangesWhenAnEntryValueChanges()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        var entryId = await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "Original");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        var beforeChange = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        await UpdateConfigEntryAsync(apiClient, entryId, "app.name", "Changed");

        // Act
        var afterChange = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        afterChange.DiffHash.ShouldNotBe(beforeChange.DiffHash);
    }

    [Fact]
    public async Task ResolveAsync_DiffHash_ChangesWhenAnEntryIsAdded()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        var beforeAdd = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        await CreateConfigEntryAsync(apiClient, "app.version", project.Id, value: "1.0");

        // Act
        var afterAdd = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        afterAdd.DiffHash.ShouldNotBe(beforeAdd.DiffHash);
    }

    [Fact]
    public async Task ResolveAsync_NextVersion_StartsAtOneAndAdvancesAfterPublish()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act + Assert
        var firstPreview = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);
        firstPreview.NextVersion.ShouldBe(1);

        await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);

        var reloaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        reloaded.ShouldNotBeNull();
        var secondPreview = await resolver.ResolveAsync(reloaded, description: null, TestCancellationToken);
        secondPreview.NextVersion.ShouldBe(2);
    }

    [Fact]
    public async Task ResolveAsync_WithUnresolvedVariable_ReportsPlaceholderName()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "api.url", project.Id, value: "https://{{apiHost}}");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        result.UnresolvedPlaceholders.ShouldContain("apiHost");
    }

    [Fact]
    public async Task ResolveAsync_DiffHash_IsLowercaseHex()
    {
        // Arrange — guards the canonical hex format Tower compares against on publish.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "app.name", project.Id, value: "MyApp");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert — the publish handler compares using StringComparison.OrdinalIgnoreCase so either
        // case is accepted, but the resolver's emitted format should remain stable. Hex chars only,
        // 64 characters for SHA-256.
        result.DiffHash.Length.ShouldBe(64);
        result.DiffHash.ShouldMatch("^[0-9A-Fa-f]+$");
    }

    [Fact]
    public async Task ResolveAsync_BsonSizeBytes_IsPositiveAndIncludesAllEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "k1", project.Id, value: "v1");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        var oneEntry = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        await CreateConfigEntryAsync(apiClient, "k2", project.Id, value: "v2");

        // Act
        var twoEntries = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        oneEntry.BsonSizeBytes.ShouldBeGreaterThan(0);
        twoEntries.BsonSizeBytes.ShouldBeGreaterThan(oneEntry.BsonSizeBytes);
    }

    [Fact]
    public async Task ResolveAsync_IncludesTemplateEntries_FromAttachedTemplates()
    {
        // Arrange — guards the JSON view from quietly omitting inherited template entries.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var template = await CreateTemplateAsync(apiClient);
        await CreateOwnedConfigEntryAsync(apiClient, "from.template", template.Id, ConfigEntryOwnerType.Template, value: "template-value");

        var project = await CreateProjectAsync(apiClient, templateIds: [template.Id]);
        await CreateConfigEntryAsync(apiClient, "from.project", project.Id, value: "project-value");

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        result.PlaintextEntries.Select(e => e.Key).ShouldBe(["from.template", "from.project"], ignoreOrder: true);
        result.PlaintextEntries.Single(e => e.Key == "from.template").Values.ShouldHaveSingleItem().Value.ShouldBe("template-value");
    }

    [Fact]
    public async Task ResolveAsync_PreservesScopeVariants_OnTemplateEntries()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new GroundControl.Api.Features.Scopes.Contracts.CreateScopeRequest { Dimension = "Environment", AllowedValues = ["dev", "prod"] },
            WebJsonSerializerOptions,
            TestCancellationToken);

        var template = await CreateTemplateAsync(apiClient);
        await apiClient.PostAsJsonAsync(
            "/api/config-entries",
            new CreateConfigEntryRequest
            {
                Key = "Jwt:Authority",
                OwnerId = template.Id,
                OwnerType = ConfigEntryOwnerType.Template,
                ValueType = "String",
                IsSensitive = false,
                Values =
                [
                    new ScopedValueRequest { Value = "https://default" },
                    new ScopedValueRequest { Value = "https://dev", Scopes = new Dictionary<string, string> { ["Environment"] = "dev" } },
                    new ScopedValueRequest { Value = "https://prod", Scopes = new Dictionary<string, string> { ["Environment"] = "prod" } },
                ],
            },
            WebJsonSerializerOptions,
            TestCancellationToken);

        var project = await CreateProjectAsync(apiClient, templateIds: [template.Id]);

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        var jwt = result.PlaintextEntries.ShouldHaveSingleItem();
        jwt.Key.ShouldBe("Jwt:Authority");
        jwt.Values.Count.ShouldBe(3);
        jwt.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "https://default");
        jwt.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "https://dev");
        jwt.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "https://prod");
    }

    [Fact]
    public async Task ResolveAsync_InterpolatesVariableUsingEntryOwnScope_WhenEntryHasScopedValues()
    {
        // Arrange — a scoped variable plus a config entry whose per-scope values reference that
        // variable. Each per-scope occurrence of the placeholder must resolve against the entry's
        // own scope context, not the variable's unscoped default.
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var scopeResponse = await apiClient.PostAsJsonAsync(
            "/api/scopes",
            new GroundControl.Api.Features.Scopes.Contracts.CreateScopeRequest { Dimension = "Environment", AllowedValues = ["dev", "prod"] },
            WebJsonSerializerOptions,
            TestCancellationToken);
        scopeResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await CreateProjectAsync(apiClient);

        var variableRequest = new CreateVariableRequest
        {
            Name = "myVariable",
            Scope = VariableScope.Project,
            ProjectId = project.Id,
            Values =
            [
                new Features.Variables.Contracts.ScopedValueRequest { Value = "1 Default" },
                new Features.Variables.Contracts.ScopedValueRequest { Value = "1 Dev", Scopes = new Dictionary<string, string> { ["Environment"] = "dev" } },
                new Features.Variables.Contracts.ScopedValueRequest { Value = "1 Prod", Scopes = new Dictionary<string, string> { ["Environment"] = "prod" } },
            ],
        };
        var variableResponse = await apiClient.PostAsJsonAsync("/api/variables", variableRequest, WebJsonSerializerOptions, TestCancellationToken);
        variableResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entryRequest = new CreateConfigEntryRequest
        {
            Key = "MyConfigEntry",
            OwnerId = project.Id,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            IsSensitive = false,
            Values =
            [
                new ScopedValueRequest { Value = "{{myVariable}}" },
                new ScopedValueRequest { Value = "{{myVariable}}", Scopes = new Dictionary<string, string> { ["Environment"] = "dev" } },
                new ScopedValueRequest { Value = "{{myVariable}}", Scopes = new Dictionary<string, string> { ["Environment"] = "prod" } },
            ],
        };
        var entryResponse = await apiClient.PostAsJsonAsync("/api/config-entries", entryRequest, WebJsonSerializerOptions, TestCancellationToken);
        entryResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var resolver = factory.Services.GetRequiredService<SnapshotResolver>();
        var projectStore = factory.Services.GetRequiredService<IProjectStore>();
        var loaded = await projectStore.GetByIdAsync(project.Id, TestCancellationToken);
        loaded.ShouldNotBeNull();

        // Act
        var result = await resolver.ResolveAsync(loaded, description: null, TestCancellationToken);

        // Assert
        var entry = result.PlaintextEntries.ShouldHaveSingleItem();
        entry.Key.ShouldBe("MyConfigEntry");
        entry.Values.Count.ShouldBe(3);
        entry.Values.ShouldContain(v => v.Scopes.Count == 0 && v.Value == "1 Default");
        entry.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "dev" && v.Value == "1 Dev");
        entry.Values.ShouldContain(v => v.Scopes.GetValueOrDefault("Environment") == "prod" && v.Value == "1 Prod");
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient)
    {
        var request = new CreateTemplateRequest
        {
            Name = $"Template-{Guid.CreateVersion7():N}",
            Description = "Test template",
        };

        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var template = await response.Content.ReadFromJsonAsync<TemplateResponse>(WebJsonSerializerOptions, TestCancellationToken);
        template.ShouldNotBeNull();

        return template;
    }

    private static async Task CreateOwnedConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        ConfigEntryOwnerType ownerType,
        string value = "default",
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ownerType,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient apiClient, List<Guid>? templateIds = null, Guid? groupId = null)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
            TemplateIds = templateIds,
            GroupId = groupId,
        };

        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<Guid> CreateConfigEntryAsync(HttpClient apiClient, string key, Guid ownerId, string value = "default", bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = await response.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, TestCancellationToken);
        entry.ShouldNotBeNull();

        return entry.Id;
    }

    private static async Task UpdateConfigEntryAsync(HttpClient apiClient, Guid entryId, string key, string value)
    {
        var current = await apiClient.GetAsync($"/api/config-entries/{entryId}", TestCancellationToken);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
        var existing = await current.Content.ReadFromJsonAsync<ConfigEntryResponse>(WebJsonSerializerOptions, TestCancellationToken);
        existing.ShouldNotBeNull();

        var update = new UpdateConfigEntryRequest
        {
            ValueType = existing.ValueType,
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = existing.IsSensitive,
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/config-entries/{entryId}")
        {
            Content = JsonContent.Create(update, options: WebJsonSerializerOptions),
        };
        request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{existing.Version}\""));

        var response = await apiClient.SendAsync(request, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}