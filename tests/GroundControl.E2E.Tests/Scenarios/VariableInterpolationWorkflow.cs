using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying variable interpolation: {{variableName}} tokens in
/// config entry values are replaced with variable values at snapshot publish time.
/// </summary>
public sealed class VariableInterpolationWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string GlobalVariableIdKey = "GlobalVariableId";
    private const string ProjectVariableIdKey = "ProjectVariableId";
    private const string SnapshotIdKey = "SnapshotId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public VariableInterpolationWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Variable Interpolation Project");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);

        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_CreateGlobalVariable() => RunStep(2, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "variable", "create",
            "--name", "base_domain",
            "--scope", "Global",
            "--value", "default=example.com");

        // Assert
        result.ShouldSucceed();
        var variable = result.ParseOutput<VariableResponse>();
        variable.Name.ShouldBe("base_domain");

        Set(GlobalVariableIdKey, variable.Id);

        var variables = await ApiClient.ListVariablesHandlerAsync(
            scope: VariableScope.Global, cancellationToken: TestCancellationToken);

        variables.Data.ShouldNotBeNull();
        variables.Data.ShouldContain(v => v.Name == "base_domain");
    });

    [Fact, Step(3)]
    public Task Step03_CreateProjectVariable() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "variable", "create",
            "--name", "api_version",
            "--scope", "Project",
            "--project-id", projectId.ToString(),
            "--value", "default=v2");

        // Assert
        result.ShouldSucceed();
        var variable = result.ParseOutput<VariableResponse>();
        variable.Name.ShouldBe("api_version");

        Set(ProjectVariableIdKey, variable.Id);

        var variables = await ApiClient.ListVariablesHandlerAsync(
            scope: VariableScope.Project,
            projectId: projectId,
            cancellationToken: TestCancellationToken);

        variables.Data.ShouldNotBeNull();
        variables.Data.ShouldContain(v => v.Name == "api_version");
    });

    [Fact, Step(4)]
    public Task Step04_AddConfigEntryReferencingVariables() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "api:base-url",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=https://{{base_domain}}/api/{{api_version}}");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        entry.Key.ShouldBe("api:base-url");
    });

    [Fact, Step(5)]
    public Task Step05_PublishSnapshotAndVerifyInterpolation() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Variable interpolation test snapshot");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        Set(SnapshotIdKey, snapshot.Id);

        var detail = await ApiClient.GetSnapshotHandlerAsync(
            projectId, snapshot.Id, decrypt: true, cancellationToken: TestCancellationToken);

        var entry = detail.Entries.FirstOrDefault(e => e.Key == "api:base-url");
        entry.ShouldNotBeNull();
        var defaultValue = entry.Values.First(v => v.Scopes.Count == 0).Value;
        defaultValue.ShouldBe("https://example.com/api/v2");
        defaultValue.ShouldNotContain("{{");
    });

    [Fact, Step(6)]
    public Task Step06_CreateClient() => RunStep(6, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-variable-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(7)]
    public Task Step07_LinkSdkReceivesInterpolatedValue() => RunStep(7, () =>
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
        var value = configuration["api:base-url"];
        value.ShouldBe("https://example.com/api/v2");
        value!.ShouldNotContain("{{");

        return Task.CompletedTask;
    });
}