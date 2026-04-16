using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying snapshot rollback: activating a previously
/// published snapshot delivers its historical values to clients.
/// </summary>
public sealed class SnapshotRollbackWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ConfigEntryIdKey = "ConfigEntryId";
    private const string ConfigEntryVersionKey = "ConfigEntryVersion";
    private const string SnapshotV1IdKey = "SnapshotV1Id";
    private const string SnapshotV2IdKey = "SnapshotV2Id";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public SnapshotRollbackWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Snapshot Rollback Project");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddConfigEntryV1() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:version",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=1.0.0");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();

        Set(ConfigEntryIdKey, entry.Id);
        Set(ConfigEntryVersionKey, entry.Version);
    });

    [Fact, Step(3)]
    public Task Step03_PublishSnapshotV1() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "v1 - initial release");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        Set(SnapshotV1IdKey, snapshot.Id);

        var detail = await ApiClient.GetSnapshotHandlerAsync(
            projectId, snapshot.Id, decrypt: true, cancellationToken: TestCancellationToken);

        var entry = detail.Entries.FirstOrDefault(e => e.Key == "app:version");
        entry.ShouldNotBeNull();
        entry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("1.0.0");
    });

    [Fact, Step(4)]
    public Task Step04_UpdateConfigEntryToV2() => RunStep(4, async () =>
    {
        // Arrange
        var entryId = Get<Guid>(ConfigEntryIdKey);
        var version = Get<long>(ConfigEntryVersionKey);

        var updateRequest = new UpdateConfigEntryRequest
        {
            ValueType = "String",
            Values =
            {
                new ScopedValueRequest { Scopes = null, Value = "2.0.0" }
            }
        };

        // Act
        GroundControlClient.SetIfMatch(version);
        var updatedEntry = await ApiClient.UpdateConfigEntryHandlerAsync(
            entryId, updateRequest, TestCancellationToken);

        // Assert
        updatedEntry.Key.ShouldBe("app:version");
        updatedEntry.Values.First(v => v.Scopes is null || v.Scopes.Count == 0).Value.ShouldBe("2.0.0");
    });

    [Fact, Step(5)]
    public Task Step05_PublishSnapshotV2() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "v2 - updated version");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        Set(SnapshotV2IdKey, snapshot.Id);

        var snapshots = await ApiClient.ListSnapshotsHandlerAsync(
            projectId, cancellationToken: TestCancellationToken);

        snapshots.Data.ShouldNotBeNull();
        snapshots.Data.Count.ShouldBe(2);

        var detail = await ApiClient.GetSnapshotHandlerAsync(
            projectId, snapshot.Id, decrypt: true, cancellationToken: TestCancellationToken);

        var entry = detail.Entries.FirstOrDefault(e => e.Key == "app:version");
        entry.ShouldNotBeNull();
        entry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("2.0.0");
    });

    [Fact, Step(6)]
    public Task Step06_RollbackToSnapshotV1() => RunStep(6, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var v1Id = Get<Guid>(SnapshotV1IdKey);

        // Act
        var activatedProject = await ApiClient.ActivateSnapshotHandlerAsync(
            projectId, v1Id, TestCancellationToken);

        // Assert
        activatedProject.ShouldNotBeNull();
        activatedProject.ActiveSnapshotId.ShouldBe(v1Id);
    });

    [Fact, Step(7)]
    public Task Step07_CreateClient() => RunStep(7, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-rollback-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(8)]
    public Task Step08_LinkSdkReceivesRolledBackValue() => RunStep(8, () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddGroundControl(opts =>
        {
            opts.ServerUrl = new Uri(Fixture.ApiBaseUrl);
            opts.ClientId = clientId.ToString();
            opts.ClientSecret = clientSecret;
            opts.StartupTimeout = TimeSpan.FromSeconds(15);
            opts.ConnectionMode = ConnectionMode.StartupOnly;
            opts.EnableLocalCache = false;
        });

        // Act
        var configuration = configBuilder.Build();

        // Assert
        configuration["app:version"].ShouldBe("1.0.0");

        return Task.CompletedTask;
    });
}