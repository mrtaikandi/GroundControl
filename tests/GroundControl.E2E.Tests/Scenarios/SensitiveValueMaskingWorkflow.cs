using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying sensitive value masking: management API masks
/// sensitive values by default, decrypts on request, and Link SDK always receives plaintext.
/// </summary>
public sealed class SensitiveValueMaskingWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string SnapshotIdKey = "SnapshotId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public SensitiveValueMaskingWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Sensitive Masking Project");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_AddSensitiveAndNonSensitiveEntries() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var sensitiveResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "secrets:api-key",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--sensitive",
            "--value", "default=s3cr3t-key");

        var plainResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:name",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=MyApp");

        // Assert
        sensitiveResult.ShouldSucceed();
        plainResult.ShouldSucceed();

        var sensitiveEntry = sensitiveResult.ParseOutput<ConfigEntryResponse>();
        sensitiveEntry.Key.ShouldBe("secrets:api-key");
        sensitiveEntry.IsSensitive.ShouldBeTrue();
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
            "--description", "Sensitive masking test snapshot");

        // Assert
        result.ShouldSucceed();
        var snapshot = result.ParseOutput<SnapshotSummaryResponse>();
        Set(SnapshotIdKey, snapshot.Id);
    });

    [Fact, Step(4)]
    public Task Step04_GetSnapshotMasked() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var snapshotId = Get<Guid>(SnapshotIdKey);

        // Act
        var masked = await ApiClient.GetSnapshotHandlerAsync(
            projectId, snapshotId, decrypt: false, cancellationToken: TestCancellationToken);

        // Assert
        var sensitiveEntry = masked.Entries.First(e => e.Key == "secrets:api-key");
        sensitiveEntry.IsSensitive.ShouldBeTrue();
        sensitiveEntry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("***");

        var plainEntry = masked.Entries.First(e => e.Key == "app:name");
        plainEntry.IsSensitive.ShouldBeFalse();
        plainEntry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("MyApp");
    });

    [Fact, Step(5)]
    public Task Step05_GetSnapshotDecrypted() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var snapshotId = Get<Guid>(SnapshotIdKey);

        // Act
        var decrypted = await ApiClient.GetSnapshotHandlerAsync(
            projectId, snapshotId, decrypt: true, cancellationToken: TestCancellationToken);

        // Assert
        var sensitiveEntry = decrypted.Entries.First(e => e.Key == "secrets:api-key");
        sensitiveEntry.IsSensitive.ShouldBeTrue();
        sensitiveEntry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("s3cr3t-key");

        var plainEntry = decrypted.Entries.First(e => e.Key == "app:name");
        plainEntry.Values.First(v => v.Scopes.Count == 0).Value.ShouldBe("MyApp");
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
            "--name", "e2e-sensitive-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(7)]
    public Task Step07_LinkSdkReceivesDecrypted() => RunStep(7, () =>
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
        configuration["secrets:api-key"].ShouldBe("s3cr3t-key");
        configuration["secrets:api-key"].ShouldNotBe("***");
        configuration["app:name"].ShouldBe("MyApp");

        return Task.CompletedTask;
    });
}