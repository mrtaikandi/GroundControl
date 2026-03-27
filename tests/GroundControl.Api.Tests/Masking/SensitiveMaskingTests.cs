using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Pagination;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shouldly;
using Xunit;
using ConfigEntryScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;
using VariableScopedValueRequest = GroundControl.Api.Features.Variables.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Masking;

public sealed class SensitiveMaskingTests : ApiHandlerTestBase
{
    public SensitiveMaskingTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    #region ConfigEntry Masking

    [Fact]
    public async Task GetConfigEntry_SensitiveField_ReturnsMaskedByDefault()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");

        // Act
        var response = await apiClient.GetAsync($"/api/config-entries/{entry.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        result.IsSensitive.ShouldBeTrue();
        result.Values.ShouldAllBe(v => v.Value == "***");
    }

    [Fact]
    public async Task GetConfigEntry_NonSensitiveField_ReturnsPlaintext()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateNonSensitiveConfigEntryAsync(apiClient, "app.name", "MyApp");

        // Act
        var response = await apiClient.GetAsync($"/api/config-entries/{entry.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        result.IsSensitive.ShouldBeFalse();
        result.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task GetConfigEntry_WithDecryptAndPermission_ReturnsPlaintext()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");

        // Act — NoAuth gives all permissions, so decrypt=true should work
        var response = await apiClient.GetAsync($"/api/config-entries/{entry.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        result.IsSensitive.ShouldBeTrue();
        result.Values.ShouldContain(v => v.Value == "s3cret!");
    }

    [Fact]
    public async Task GetConfigEntry_DecryptWithPermission_CreatesAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");

        // Act
        await apiClient.GetAsync($"/api/config-entries/{entry.Id}?decrypt=true", TestCancellationToken);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.EntityId == entry.Id && r.Action == "Decrypted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
        auditRecords[0].PerformedBy.ShouldBe(Guid.Empty); // NoAuth mode
    }

    [Fact]
    public async Task GetConfigEntry_NonSensitiveWithDecrypt_ReturnsPlaintext()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateNonSensitiveConfigEntryAsync(apiClient, "app.name", "MyApp");

        // Act — decrypt=true should have no effect on non-sensitive values
        var response = await apiClient.GetAsync($"/api/config-entries/{entry.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        result.IsSensitive.ShouldBeFalse();
        result.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task GetConfigEntry_SensitiveWithoutDecryptParam_DoesNotCreateAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var entry = await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");

        // Act — no decrypt param
        await apiClient.GetAsync($"/api/config-entries/{entry.Id}", TestCancellationToken);

        // Assert — no Decrypted audit record should exist
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "ConfigEntry" && r.EntityId == entry.Id && r.Action == "Decrypted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetConfigEntry_DecryptWithoutPermission_ReturnsMaskedSilently()
    {
        // Arrange — create the entry with full permissions (NoAuth)
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();
        var entry = await CreateSensitiveConfigEntryAsync(adminClient, "db.password", "s3cret!");

        // Create a client whose principal lacks the decrypt permission
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddTransient<IClaimsTransformation, StripDecryptPermissionTransformation>();
            });
        });
        using var limitedClient = limitedFactory.CreateClient();

        // Act — decrypt=true is requested but the user lacks the permission
        var response = await limitedClient.GetAsync($"/api/config-entries/{entry.Id}?decrypt=true", TestCancellationToken);

        // Assert — silent mask, no error
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
        result.Values.ShouldAllBe(v => v.Value == "***");
    }

    [Fact]
    public async Task ListConfigEntries_MasksSensitiveValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");
        await CreateNonSensitiveConfigEntryAsync(apiClient, "app.name", "MyApp");

        // Act
        var response = await apiClient.GetAsync("/api/config-entries", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadRequiredJsonAsync<PaginatedResponse<ConfigEntryResponse>>(response, TestCancellationToken);
        page.Data.Count.ShouldBe(2);

        var sensitive = page.Data.First(e => e.Key == "db.password");
        sensitive.Values.ShouldAllBe(v => v.Value == "***");

        var normal = page.Data.First(e => e.Key == "app.name");
        normal.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task ListConfigEntries_WithDecrypt_ReturnsPlaintextForSensitive()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateSensitiveConfigEntryAsync(apiClient, "db.password", "s3cret!");

        // Act
        var response = await apiClient.GetAsync("/api/config-entries?decrypt=true", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadRequiredJsonAsync<PaginatedResponse<ConfigEntryResponse>>(response, TestCancellationToken);
        var sensitive = page.Data.First(e => e.Key == "db.password");
        sensitive.Values.ShouldContain(v => v.Value == "s3cret!");
    }

    #endregion

    #region Variable Masking

    [Fact]
    public async Task GetVariable_SensitiveField_ReturnsMaskedByDefault()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var variable = await CreateSensitiveVariableAsync(apiClient, "db_password", "s3cret!");

        // Act
        var response = await apiClient.GetAsync($"/api/variables/{variable.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
        result.IsSensitive.ShouldBeTrue();
        result.Values.ShouldAllBe(v => v.Value == "***");
    }

    [Fact]
    public async Task GetVariable_WithDecryptAndPermission_ReturnsPlaintext()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var variable = await CreateSensitiveVariableAsync(apiClient, "db_password", "s3cret!");

        // Act
        var response = await apiClient.GetAsync($"/api/variables/{variable.Id}?decrypt=true", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
        result.Values.ShouldContain(v => v.Value == "s3cret!");
    }

    [Fact]
    public async Task GetVariable_DecryptWithPermission_CreatesAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var variable = await CreateSensitiveVariableAsync(apiClient, "db_password", "s3cret!");

        // Act
        await apiClient.GetAsync($"/api/variables/{variable.Id}?decrypt=true", TestCancellationToken);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Variable" && r.EntityId == variable.Id && r.Action == "Decrypted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ListVariables_MasksSensitiveValues()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        await CreateSensitiveVariableAsync(apiClient, "db_password", "s3cret!");
        await CreateNonSensitiveVariableAsync(apiClient, "app_name", "MyApp");

        // Act
        var response = await apiClient.GetAsync("/api/variables", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadRequiredJsonAsync<PaginatedResponse<VariableResponse>>(response, TestCancellationToken);
        page.Data.Count.ShouldBe(2);

        var sensitive = page.Data.First(v => v.Name == "db_password");
        sensitive.Values.ShouldAllBe(v => v.Value == "***");

        var normal = page.Data.First(v => v.Name == "app_name");
        normal.Values.ShouldContain(v => v.Value == "MyApp");
    }

    #endregion

    #region Snapshot Masking

    [Fact]
    public async Task GetSnapshot_SensitiveValues_ReturnsMaskedByDefault()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryForProjectAsync(apiClient, "db.password", project.Id, "s3cret!", isSensitive: true);
        await CreateConfigEntryForProjectAsync(apiClient, "app.name", project.Id, "MyApp");

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);

        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var published = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync(
            $"/api/projects/{project.Id}/snapshots/{published.Id}",
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshot = await ReadRequiredJsonAsync<SnapshotResponse>(response, TestCancellationToken);

        var sensitiveEntry = snapshot.Entries.First(e => e.Key == "db.password");
        sensitiveEntry.IsSensitive.ShouldBeTrue();
        sensitiveEntry.Values.ShouldAllBe(v => v.Value == "***");

        var normalEntry = snapshot.Entries.First(e => e.Key == "app.name");
        normalEntry.Values.ShouldContain(v => v.Value == "MyApp");
    }

    [Fact]
    public async Task GetSnapshot_DecryptWithPermission_CreatesAuditRecord()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryForProjectAsync(apiClient, "db.password", project.Id, "s3cret!", isSensitive: true);

        var publishResponse = await apiClient.PostAsJsonAsync(
            $"/api/projects/{project.Id}/snapshots",
            new PublishSnapshotRequest(),
            WebJsonSerializerOptions,
            TestCancellationToken);

        publishResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var published = await ReadRequiredJsonAsync<SnapshotSummaryResponse>(publishResponse, TestCancellationToken);

        // Act
        await apiClient.GetAsync($"/api/projects/{project.Id}/snapshots/{published.Id}?decrypt=true", TestCancellationToken);

        // Assert
        var auditRecords = await factory.Database.GetCollection<AuditRecord>("audit_records")
            .Find(r => r.EntityType == "Snapshot" && r.EntityId == published.Id && r.Action == "Decrypted")
            .ToListAsync(TestCancellationToken);

        auditRecords.Count.ShouldBe(1);
    }

    #endregion

    #region Helpers

    private static async Task<ConfigEntryResponse> CreateSensitiveConfigEntryAsync(
        HttpClient apiClient,
        string key,
        string value)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = value }],
            IsSensitive = true,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
    }

    private static async Task<ConfigEntryResponse> CreateNonSensitiveConfigEntryAsync(
        HttpClient apiClient,
        string key,
        string value)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = Guid.CreateVersion7(),
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = value }],
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<ConfigEntryResponse>(response, TestCancellationToken);
    }

    private static async Task<VariableResponse> CreateSensitiveVariableAsync(
        HttpClient apiClient,
        string name,
        string value)
    {
        var request = new CreateVariableRequest
        {
            Name = name,
            Scope = VariableScope.Global,
            Values = [new VariableScopedValueRequest { Value = value }],
            IsSensitive = true,
        };

        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
    }

    private static async Task<VariableResponse> CreateNonSensitiveVariableAsync(
        HttpClient apiClient,
        string name,
        string value)
    {
        var request = new CreateVariableRequest
        {
            Name = name,
            Scope = VariableScope.Global,
            Values = [new VariableScopedValueRequest { Value = value }],
        };

        var response = await apiClient.PostAsJsonAsync("/api/variables", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<VariableResponse>(response, TestCancellationToken);
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient apiClient)
    {
        var group = await CreateGroupAsync(apiClient);
        var template = await CreateTemplateAsync(apiClient, group.Id);
        var request = new CreateProjectRequest
        {
            Name = $"masking-test-project-{Guid.CreateVersion7():N}",
            GroupId = group.Id,
            TemplateIds = [template.Id],
        };

        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<ProjectResponse>(response, TestCancellationToken);
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient apiClient)
    {
        var request = new CreateGroupRequest
        {
            Name = $"masking-test-group-{Guid.CreateVersion7():N}",
            Description = "test group",
        };

        var response = await apiClient.PostAsJsonAsync("/api/groups", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<GroupResponse>(response, TestCancellationToken);
    }

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient apiClient, Guid groupId)
    {
        var request = new CreateTemplateRequest
        {
            Name = $"masking-test-template-{Guid.CreateVersion7():N}",
            Description = "test template",
            GroupId = groupId,
        };

        var response = await apiClient.PostAsJsonAsync("/api/templates", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRequiredJsonAsync<TemplateResponse>(response, TestCancellationToken);
    }

    private static async Task CreateConfigEntryForProjectAsync(
        HttpClient apiClient,
        string key,
        Guid projectId,
        string value = "default",
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = projectId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ConfigEntryScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.EnsureSuccessStatusCode();
    }

    #endregion

    /// <summary>
    /// Claims transformation that strips the <c>sensitive_values:decrypt</c> permission claim,
    /// simulating a user who lacks decrypt permission.
    /// </summary>
    private sealed class StripDecryptPermissionTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is ClaimsIdentity identity)
            {
                var decryptClaim = identity.FindFirst(
                    c => c.Type == "permission" && c.Value == Permissions.SensitiveValuesDecrypt);

                if (decryptClaim is not null)
                {
                    identity.RemoveClaim(decryptClaim);
                }
            }

            return Task.FromResult(principal);
        }
    }
}