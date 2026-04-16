using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying that snapshot publish fails with 422 when config
/// entries contain unresolved variable placeholders, and succeeds once the
/// variable is defined.
/// </summary>
public sealed class UnresolvedVariableBlocksPublishWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ConfigEntryIdKey = "ConfigEntryId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public UnresolvedVariableBlocksPublishWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Unresolved Variable");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddConfigEntryWithPlaceholder() => RunStep(2, async () =>
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
            "--value", "default=https://{{undefined_host}}/api");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        Set(ConfigEntryIdKey, entry.Id);
    });

    [Fact, Step(3)]
    public Task Step03_PublishFailsWith422() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var exception = await Should.ThrowAsync<GroundControlApiClientException>(async () =>
            await ApiClient.PublishSnapshotHandlerAsync(
                projectId,
                new PublishSnapshotRequest { Description = "Should fail - unresolved var" },
                TestCancellationToken));

        // Assert
        exception.StatusCode.ShouldBe(422);
        exception.Response.ShouldNotBeNull();
        exception.Response.ShouldContain("undefined_host");
    });

    [Fact, Step(4)]
    public Task Step04_CreateMissingVariable() => RunStep(4, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "variable", "create",
            "--name", "undefined_host",
            "--scope", "Global",
            "--value", "default=api.example.com");

        // Assert
        result.ShouldSucceed();
        var variable = result.ParseOutput<VariableResponse>();
        variable.Name.ShouldBe("undefined_host");
    });

    [Fact, Step(5)]
    public Task Step05_PublishNowSucceeds() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Should succeed - variable resolved");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        snapshot.ProjectId.ShouldBe(projectId);
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
            "--name", "e2e-unresolved-var-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
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
        configuration["api:base-url"].ShouldBe("https://api.example.com/api");

        return Task.CompletedTask;
    });
}