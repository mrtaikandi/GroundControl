using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying scope resolution: clients with different scope
/// assignments receive different values for the same config key.
/// </summary>
public sealed class ScopedValueResolutionWorkflow : EndToEndTestBase
{
    private const string ScopeIdKey = "ScopeId";
    private const string ProjectIdKey = "ProjectId";
    private const string SnapshotIdKey = "SnapshotId";
    private const string ProdClientIdKey = "ProdClientId";
    private const string ProdClientSecretKey = "ProdClientSecret";
    private const string StagingClientIdKey = "StagingClientId";
    private const string StagingClientSecretKey = "StagingClientSecret";

    public ScopedValueResolutionWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateScope() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "scope", "create",
            "--dimension", "env",
            "--values", "prod,staging");

        // Assert
        result.ShouldSucceed();

        var scope = result.ParseOutput<ScopeResponse>();
        scope.Dimension.ShouldBe("env");

        var scopes = await ApiClient.ListScopesHandlerAsync(cancellationToken: TestCancellationToken);
        scopes.Data.ShouldNotBeNull();
        scopes.Data.ShouldContain(s => s.Dimension == "env");

        Set(ScopeIdKey, scope.Id);
    });

    [Fact, Step(2)]
    public Task Step02_CreateProject() => RunStep(2, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Scoped Resolution Project");

        // Assert
        result.ShouldSucceed();

        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);

        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(3)]
    public Task Step03_AddScopedConfigEntry() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "db:url",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=base-url",
            "--value", "env:prod=prod-url",
            "--value", "env:staging=staging-url");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        entry.Key.ShouldBe("db:url");

        var entries = await ApiClient.ListConfigEntriesHandlerAsync(
            ownerId: projectId,
            ownerType: ConfigEntryOwnerType.Project,
            cancellationToken: TestCancellationToken);

        entries.Data.ShouldNotBeNull();
        entries.Data.Count.ShouldBe(1);
        entries.Data.First().Values.Count.ShouldBe(3);
    });

    [Fact, Step(4)]
    public Task Step04_PublishSnapshot() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Scoped resolution test snapshot");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        snapshot.ProjectId.ShouldBe(projectId);

        Set(SnapshotIdKey, snapshot.Id);
    });

    [Fact, Step(5)]
    public Task Step05_CreateProdClient() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-scoped-client-prod",
            "--scopes", "env=prod");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ProdClientIdKey, client.Id);
        Set(ProdClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(6)]
    public Task Step06_CreateStagingClient() => RunStep(6, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-scoped-client-staging",
            "--scopes", "env=staging");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(StagingClientIdKey, client.Id);
        Set(StagingClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(7)]
    public Task Step07_ProdClientReceivesProdValue() => RunStep(7, () =>
    {
        // Arrange
        var clientId = Get<Guid>(ProdClientIdKey);
        var clientSecret = Get<string>(ProdClientSecretKey);

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
        configuration["db:url"].ShouldBe("prod-url");

        return Task.CompletedTask;
    });

    [Fact, Step(8)]
    public Task Step08_StagingClientReceivesStagingValue() => RunStep(8, () =>
    {
        // Arrange
        var clientId = Get<Guid>(StagingClientIdKey);
        var clientSecret = Get<string>(StagingClientSecretKey);

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
        configuration["db:url"].ShouldBe("staging-url");

        return Task.CompletedTask;
    });
}