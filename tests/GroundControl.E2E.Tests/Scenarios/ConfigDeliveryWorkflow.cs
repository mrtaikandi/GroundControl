using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow: CLI creates a project, adds config entries, publishes a snapshot,
/// creates a client, and the Link SDK receives the configuration.
/// </summary>
public sealed class ConfigDeliveryWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public ConfigDeliveryWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Config Delivery");

        // Assert
        result.ShouldSucceed();

        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);
        project.Name.ShouldBe("E2E Config Delivery");

        // Verify via API client
        var apiProject = await ApiClient.GetProjectHandlerAsync(project.Id, TestCancellationToken);
        apiProject.Name.ShouldBe("E2E Config Delivery");

        // Store for subsequent steps
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddConfigEntries() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act - create two config entries
        var result1 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:name",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=MyApp");

        var result2 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:version",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=1.0.0");

        // Assert
        result1.ShouldSucceed();
        result2.ShouldSucceed();

        var entry1 = result1.ParseOutput<ConfigEntryResponse>();
        entry1.Key.ShouldBe("app:name");

        var entry2 = result2.ParseOutput<ConfigEntryResponse>();
        entry2.Key.ShouldBe("app:version");

        // Verify via API client
        var entries = await ApiClient.ListConfigEntriesHandlerAsync(
            ownerId: projectId,
            ownerType: ConfigEntryOwnerType.Project,
            cancellationToken: TestCancellationToken);

        entries.Data.ShouldNotBeNull();
        entries.Data.Count.ShouldBe(2);
    });

    [Fact, Step(3)]
    public Task Step03_PublishSnapshot() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "E2E test snapshot");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        snapshot.ProjectId.ShouldBe(projectId);

        // Verify via API client
        var snapshots = await ApiClient.ListSnapshotsHandlerAsync(
            projectId,
            cancellationToken: TestCancellationToken);

        snapshots.Data.ShouldNotBeNull();
        snapshots.Data.Count.ShouldBe(1);
    });

    [Fact, Step(4)]
    public Task Step04_CreateApiClient() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-sdk-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.Id.ShouldNotBe(Guid.Empty);
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        // Verify via API client
        var apiClient = await ApiClient.GetClientHandlerAsync(
            projectId, client.Id, TestCancellationToken);

        apiClient.Name.ShouldBe("e2e-sdk-client");

        // Store for Link SDK step
        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(5)]
    public Task Step05_LinkSdkReceivesConfig() => RunStep(5, () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);

        // Act
        using var provider = CreateLinkProvider(clientId, clientSecret);
        provider.Load();

        // Assert
        provider.TryGet("app:name", out var appName).ShouldBeTrue("Expected 'app:name' to be present in configuration");
        appName.ShouldBe("MyApp");

        provider.TryGet("app:version", out var appVersion).ShouldBeTrue("Expected 'app:version' to be present in configuration");
        appVersion.ShouldBe("1.0.0");

        return Task.CompletedTask;
    });
}