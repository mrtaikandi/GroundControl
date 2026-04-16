using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying multi-dimensional scope isolation across multiple
/// clients with different scope assignments.
/// </summary>
public sealed class MultiClientScopeIsolationWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ProdUsClientIdKey = "ProdUsClientId";
    private const string ProdUsClientSecretKey = "ProdUsClientSecret";
    private const string ProdEuClientIdKey = "ProdEuClientId";
    private const string ProdEuClientSecretKey = "ProdEuClientSecret";
    private const string StagingClientIdKey = "StagingClientId";
    private const string StagingClientSecretKey = "StagingClientSecret";

    public MultiClientScopeIsolationWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateScopes() => RunStep(1, async () =>
    {
        // Arrange & Act
        var envResult = await Cli.RunAsync(TestCancellationToken,
            "scope", "create",
            "--dimension", "env",
            "--values", "dev,staging,prod");

        var regionResult = await Cli.RunAsync(TestCancellationToken,
            "scope", "create",
            "--dimension", "region",
            "--values", "us,eu,ap");

        // Assert
        envResult.ShouldSucceed();
        regionResult.ShouldSucceed();
    });

    [Fact, Step(2)]
    public Task Step02_CreateProject() => RunStep(2, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Scope Isolation");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(3)]
    public Task Step03_AddMultiScopedConfigEntry() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "db:host",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=db-default.internal",
            "--value", "env:prod=db-prod.internal",
            "--value", "env:staging=db-staging.internal",
            "--value", "env:prod,region:us=db-prod-us.internal");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        entry.Key.ShouldBe("db:host");
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
            "--description", "Scope isolation test");

        // Assert
        result.ShouldSucceed();
    });

    [Fact, Step(5)]
    public Task Step05_CreateThreeClients() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var prodUsResult = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "client-prod-us",
            "--scopes", "env=prod,region=us");
        var prodEuResult = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "client-prod-eu",
            "--scopes", "env=prod,region=eu");
        var stagingResult = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "client-staging",
            "--scopes", "env=staging");

        // Assert
        prodUsResult.ShouldSucceed();
        prodEuResult.ShouldSucceed();
        stagingResult.ShouldSucceed();

        var prodUs = prodUsResult.ParseOutput<CreateClientResponse>();
        var prodEu = prodEuResult.ParseOutput<CreateClientResponse>();
        var staging = stagingResult.ParseOutput<CreateClientResponse>();

        Set(ProdUsClientIdKey, prodUs.Id);
        Set(ProdUsClientSecretKey, prodUs.ClientSecret);
        Set(ProdEuClientIdKey, prodEu.Id);
        Set(ProdEuClientSecretKey, prodEu.ClientSecret);
        Set(StagingClientIdKey, staging.Id);
        Set(StagingClientSecretKey, staging.ClientSecret);
    });

    [Fact, Step(6)]
    public Task Step06_ProdUsClientGetsExactMatch() => RunStep(6, () =>
    {
        // Arrange & Act & Assert — exact match on env:prod + region:us wins
        WithClientConfiguration(
            Get<Guid>(ProdUsClientIdKey),
            Get<string>(ProdUsClientSecretKey),
            configuration => configuration["db:host"].ShouldBe("db-prod-us.internal"));

        return Task.CompletedTask;
    });

    [Fact, Step(7)]
    public Task Step07_ProdEuClientFallsBackToEnvProd() => RunStep(7, () =>
    {
        // Arrange & Act & Assert — falls back to env:prod when region:eu has no override
        WithClientConfiguration(
            Get<Guid>(ProdEuClientIdKey),
            Get<string>(ProdEuClientSecretKey),
            configuration => configuration["db:host"].ShouldBe("db-prod.internal"));

        return Task.CompletedTask;
    });

    [Fact, Step(8)]
    public Task Step08_StagingClientGetsEnvStaging() => RunStep(8, () =>
    {
        // Arrange & Act & Assert — env:staging match
        WithClientConfiguration(
            Get<Guid>(StagingClientIdKey),
            Get<string>(StagingClientSecretKey),
            configuration => configuration["db:host"].ShouldBe("db-staging.internal"));

        return Task.CompletedTask;
    });

    private void WithClientConfiguration(Guid clientId, string clientSecret, Action<IConfiguration> assert)
    {
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

        var configuration = configBuilder.Build();
        try
        {
            assert(configuration);
        }
        finally
        {
            (configuration as IDisposable)?.Dispose();
        }
    }
}