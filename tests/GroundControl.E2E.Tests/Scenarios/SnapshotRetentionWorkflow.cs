using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying snapshot retention cleanup: after publishing more
/// snapshots than the configured retention count, old snapshots are purged.
/// </summary>
public sealed class SnapshotRetentionWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ConfigEntryIdKey = "ConfigEntryId";

    public SnapshotRetentionWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Retention Test");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddConfigEntry() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:setting",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=v1");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        Set(ConfigEntryIdKey, entry.Id);
    });

    [Fact, Step(3)]
    public Task Step03_PublishFiveSnapshotsAndVerifyRetention() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var entryId = Get<Guid>(ConfigEntryIdKey);

        var publish1 = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Retention test v1");
        publish1.ShouldSucceed();

        // Act — update config entry and republish four more times
        long currentVersion = 1;
        for (var i = 2; i <= 5; i++)
        {
            var update = await Cli.RunAsync(TestCancellationToken,
                "config-entry", "update",
                entryId.ToString(),
                "--value-type", "String",
                "--value", $"default=v{i}",
                "--version", currentVersion.ToString());
            update.ShouldSucceed();
            var updated = update.ParseOutput<ConfigEntryResponse>();
            currentVersion = updated.Version;

            var publish = await Cli.RunAsync(TestCancellationToken,
                "snapshot", "publish",
                "--project-id", projectId.ToString(),
                "--description", $"Retention test v{i}");
            publish.ShouldSucceed();
        }

        // Assert
        var snapshots = await ApiClient.ListSnapshotsHandlerAsync(
            projectId,
            sortField: "snapshotVersion",
            sortOrder: "desc",
            cancellationToken: TestCancellationToken);

        snapshots.Data.ShouldNotBeNull();
        snapshots.Data.Count.ShouldBe(3);

        var versions = snapshots.Data.Select(s => s.SnapshotVersion).OrderByDescending(v => v).ToList();
        versions[0].ShouldBe(5);
        versions[1].ShouldBe(4);
        versions[2].ShouldBe(3);

        versions.ShouldNotContain(1);
        versions.ShouldNotContain(2);
    });
}