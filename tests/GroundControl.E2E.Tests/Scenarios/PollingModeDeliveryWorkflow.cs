using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying polling-mode delivery: a Link SDK client with a
/// short polling interval picks up config changes within one or two polling cycles.
/// </summary>
public sealed class PollingModeDeliveryWorkflow : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ConfigEntryIdKey = "ConfigEntryId";
    private const string ConfigEntryVersionKey = "ConfigEntryVersion";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public PollingModeDeliveryWorkflow(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "E2E Polling Delivery");

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
            "--key", "app:version",
            "--owner-id", projectId.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=1.0");

        // Assert
        result.ShouldSucceed();
        var entry = result.ParseOutput<ConfigEntryResponse>();
        Set(ConfigEntryIdKey, entry.Id);
        Set(ConfigEntryVersionKey, entry.Version);
    });

    [Fact, Step(3)]
    public Task Step03_PublishInitialSnapshot() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Polling test initial");

        // Assert
        result.ShouldSucceed();
    });

    [Fact, Step(4)]
    public Task Step04_CreateClient() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "e2e-polling-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(5)]
    public Task Step05_PollingClientReceivesUpdate() => RunStep(5, async () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);
        var projectId = Get<Guid>(ProjectIdKey);
        var configEntryId = Get<Guid>(ConfigEntryIdKey);
        var configEntryVersion = Get<long>(ConfigEntryVersionKey);

        var hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.Sources.Clear();
            configBuilder.AddGroundControl(opts =>
            {
                opts.ServerUrl = new Uri(Fixture.ApiBaseUrl);
                opts.ClientId = clientId.ToString();
                opts.ClientSecret = clientSecret;
                opts.StartupTimeout = TimeSpan.FromSeconds(15);
                opts.ConnectionMode = ConnectionMode.Polling;
                opts.PollingInterval = TimeSpan.FromSeconds(2);
                opts.EnableLocalCache = false;
            });
        });

        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddGroundControl((IConfigurationRoot)context.Configuration);
        });

        using var host = hostBuilder.Build();
        await host.StartAsync(TestCancellationToken);

        try
        {
            // Act
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            configuration["app:version"].ShouldBe("1.0");

            GroundControlClient.SetIfMatch(configEntryVersion);
            await ApiClient.UpdateConfigEntryHandlerAsync(
                configEntryId,
                new UpdateConfigEntryRequest
                {
                    ValueType = "String",
                    Values =
                    {
                        new ScopedValueRequest { Scopes = null, Value = "2.0" }
                    }
                },
                TestCancellationToken);

            await ApiClient.PublishSnapshotHandlerAsync(
                projectId,
                new PublishSnapshotRequest { Description = "Polling test update" },
                TestCancellationToken);

            // Assert — poll for update via polling strategy
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            var updated = false;
            while (DateTime.UtcNow < deadline)
            {
                if (configuration["app:version"] == "2.0")
                {
                    updated = true;
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), TestCancellationToken);
            }

            updated.ShouldBeTrue($"Configuration was not updated via polling within 10s. Current value: {configuration["app:version"]}");
            configuration["app:version"].ShouldBe("2.0");
        }
        finally
        {
            await host.StopAsync(TestCancellationToken);
        }
    });
}