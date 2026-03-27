using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Link.Tests.Infrastructure;
using GroundControl.Persistence.Contracts;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Link.Tests.Integration;

public sealed class SdkIntegrationTests : SdkIntegrationTestBase
{
    public SdkIntegrationTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Sdk_InitialLoad_ReceivesConfigFromServer()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "app.name", project.Id, value: "MyApp");
        await CreateConfigEntryAsync(adminClient, "app.version", project.Id, value: "2.0.0");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "sdk-test-client");

        using var sdkHttpClient = factory.CreateClient();
        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("app.name", out var name).ShouldBeTrue();
        name.ShouldBe("MyApp");
        provider.TryGet("app.version", out var version).ShouldBeTrue();
        version.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task Sdk_PollingMode_LoadsConfigViaRest()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "db.host", project.Id, value: "prod-db.example.com");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "polling-client");

        using var sdkHttpClient = factory.CreateClient();
        var options = new GroundControlOptions
        {
            ServerUrl = sdkHttpClient.BaseAddress!.ToString(),
            ClientId = client.Id.ToString(),
            ClientSecret = client.ClientSecret,
            StartupTimeout = TimeSpan.FromSeconds(10),
            ConnectionMode = ConnectionMode.Polling,
            PollingInterval = TimeSpan.FromHours(1),
            EnableLocalCache = false,
        };

        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret, options);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("db.host", out var host).ShouldBeTrue();
        host.ShouldBe("prod-db.example.com");
    }

    [Fact]
    public async Task Sdk_RealTimeUpdate_ReloadsWhenNewSnapshotPublished()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "feature.enabled", project.Id, value: "false");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "realtime-client");

        using var sdkHttpClient = factory.CreateClient();
        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret);

        provider.Load();
        provider.TryGet("feature.enabled", out var initialValue).ShouldBeTrue();
        initialValue.ShouldBe("false");

        // Set up reload listener
        var reloadTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changeToken = provider.GetReloadToken();
        changeToken.RegisterChangeCallback(_ => reloadTriggered.TrySetResult(true), null);

        // Act — update config and publish new snapshot
        await CreateConfigEntryAsync(adminClient, "feature.new-key", project.Id, value: "new-value");
        await PublishSnapshotAsync(adminClient, project.Id);

        // Assert — wait for reload with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var completed = await Task.WhenAny(reloadTriggered.Task, Task.Delay(Timeout.Infinite, cts.Token));
        completed.ShouldBe(reloadTriggered.Task, "OnReload should have been triggered within 5 seconds");

        provider.TryGet("feature.new-key", out var newValue).ShouldBeTrue();
        newValue.ShouldBe("new-value");
    }

    [Fact]
    public async Task Sdk_MultipleConfigEntries_AllAvailableInConfiguration()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "Logging:LogLevel:Default", project.Id, value: "Warning");
        await CreateConfigEntryAsync(adminClient, "Database:Host", project.Id, value: "localhost");
        await CreateConfigEntryAsync(adminClient, "Database:Port", project.Id, value: "5432");
        await CreateConfigEntryAsync(adminClient, "FeatureFlags:DarkMode", project.Id, value: "true");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "multi-entry-client");

        using var sdkHttpClient = factory.CreateClient();
        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("Logging:LogLevel:Default", out var logLevel).ShouldBeTrue();
        logLevel.ShouldBe("Warning");
        provider.TryGet("Database:Host", out var dbHost).ShouldBeTrue();
        dbHost.ShouldBe("localhost");
        provider.TryGet("Database:Port", out var dbPort).ShouldBeTrue();
        dbPort.ShouldBe("5432");
        provider.TryGet("FeatureFlags:DarkMode", out var darkMode).ShouldBeTrue();
        darkMode.ShouldBe("true");
    }

    [Fact]
    public async Task Sdk_CaseInsensitiveKeyLookup_WorksWithRealServer()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "MySection:MyKey", project.Id, value: "MyValue");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "case-client");

        using var sdkHttpClient = factory.CreateClient();
        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("mysection:mykey", out var lower).ShouldBeTrue();
        lower.ShouldBe("MyValue");
        provider.TryGet("MYSECTION:MYKEY", out var upper).ShouldBeTrue();
        upper.ShouldBe("MyValue");
    }

    [Fact]
    public async Task Sdk_NoActiveSnapshot_StartsWithEmptyConfig()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        var client = await CreateApiClientAsync(adminClient, project.Id, "no-snapshot-client");

        using var sdkHttpClient = factory.CreateClient();
        var options = new GroundControlOptions
        {
            ServerUrl = sdkHttpClient.BaseAddress!.ToString(),
            ClientId = client.Id.ToString(),
            ClientSecret = client.ClientSecret,
            StartupTimeout = TimeSpan.FromSeconds(2),
            ConnectionMode = ConnectionMode.SseWithPollingFallback,
            PollingInterval = TimeSpan.FromHours(1),
            EnableLocalCache = false,
        };

        using var provider = CreateSdkProvider(sdkHttpClient, client.Id, client.ClientSecret, options);

        // Act
        provider.Load();

        // Assert
        provider.TryGet("any.key", out _).ShouldBeFalse();
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient httpClient)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
        };

        var response = await httpClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<CreateClientResponse> CreateApiClientAsync(HttpClient httpClient, Guid projectId, string name)
    {
        var request = new CreateClientRequest { Name = name };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task PublishSnapshotAsync(HttpClient httpClient, Guid projectId)
    {
        var request = new PublishSnapshotRequest { Description = "Test snapshot" };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/snapshots", request, WebJsonSerializerOptions, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient httpClient,
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
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await httpClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

}