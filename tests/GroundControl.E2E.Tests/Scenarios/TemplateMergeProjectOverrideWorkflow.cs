using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying template/project merge precedence: project entries
/// override template entries by key, and template-only entries pass through.
/// </summary>
public sealed class TemplateMergeProjectOverrideWorkflow : EndToEndTestBase
{
    private const string TemplateIdKey = "TemplateId";
    private const string ProjectIdKey = "ProjectId";
    private const string SnapshotIdKey = "SnapshotId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public TemplateMergeProjectOverrideWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateTemplate() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "template", "create",
            "--name", "E2E Base Template");

        // Assert
        result.ShouldSucceed();

        var template = result.ParseOutput<TemplateResponse>();
        template.Id.ShouldNotBe(Guid.Empty);
        template.Name.ShouldBe("E2E Base Template");

        Set(TemplateIdKey, template.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddTemplateConfigEntries() => RunStep(2, async () =>
    {
        // Arrange
        var templateId = Get<Guid>(TemplateIdKey);

        // Act
        var result1 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:name",
            "--owner-id", templateId.ToString(),
            "--owner-type", "Template",
            "--value-type", "String",
            "--value", "default=TemplateApp");

        var result2 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:feature",
            "--owner-id", templateId.ToString(),
            "--owner-type", "Template",
            "--value-type", "String",
            "--value", "default=from-template");

        // Assert
        result1.ShouldSucceed();
        result2.ShouldSucceed();

        result1.ParseOutput<ConfigEntryResponse>().Key.ShouldBe("app:name");
        result2.ParseOutput<ConfigEntryResponse>().Key.ShouldBe("app:feature");

        var entries = await ApiClient.ListConfigEntriesHandlerAsync(
            ownerId: templateId,
            ownerType: ConfigEntryOwnerType.Template,
            cancellationToken: TestCancellationToken);

        entries.Data.ShouldNotBeNull();
        entries.Data.Count.ShouldBe(2);
    });

    [Fact, Step(3)]
    public Task Step03_CreateProject() => RunStep(3, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Template Override Project");

        // Assert
        result.ShouldSucceed();

        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);

        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(4)]
    public Task Step04_AssociateTemplateWithProject() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var templateId = Get<Guid>(TemplateIdKey);

        // Act
        var updatedProject = await ApiClient.AddProjectTemplateHandlerAsync(
            projectId, templateId, TestCancellationToken);

        // Assert
        updatedProject.ShouldNotBeNull();
        updatedProject.TemplateIds.ShouldContain(templateId);
    });

    [Fact, Step(5)]
    public Task Step05_AddProjectConfigEntries() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result1 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:name",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=ProjectApp");

        var result2 = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:env",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=production");

        // Assert
        result1.ShouldSucceed();
        result2.ShouldSucceed();

        var entries = await ApiClient.ListConfigEntriesHandlerAsync(
            ownerId: projectId,
            ownerType: ConfigEntryOwnerType.Project,
            cancellationToken: TestCancellationToken);

        entries.Data.ShouldNotBeNull();
        entries.Data.Count.ShouldBe(2);
    });

    [Fact, Step(6)]
    public Task Step06_PublishSnapshot() => RunStep(6, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Template merge test snapshot");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        snapshot.ProjectId.ShouldBe(projectId);

        Set(SnapshotIdKey, snapshot.Id);

        var snapshots = await ApiClient.ListSnapshotsHandlerAsync(
            projectId, cancellationToken: TestCancellationToken);

        snapshots.Data.ShouldNotBeNull();
        snapshots.Data.Count.ShouldBe(1);
    });

    [Fact, Step(7)]
    public Task Step07_CreateApiClient() => RunStep(7, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-template-merge-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.Id.ShouldNotBe(Guid.Empty);
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(8)]
    public Task Step08_LinkSdkReceivesMergedConfig() => RunStep(8, () =>
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
        configuration["app:name"].ShouldBe("ProjectApp");
        configuration["app:feature"].ShouldBe("from-template");
        configuration["app:env"].ShouldBe("production");

        return Task.CompletedTask;
    });
}