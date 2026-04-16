using System.Net;
using System.Net.Http;
using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying the client activation lifecycle: deactivated
/// clients are rejected with 401, and reactivation restores access.
/// </summary>
public sealed class DeactivatedClientRejection : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public DeactivatedClientRejection(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_SetupProjectAndSnapshot() => RunStep(1, async () =>
    {
        // Arrange & Act
        var projectResult = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "Client Activation Lifecycle");
        projectResult.ShouldSucceed();
        var project = projectResult.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);

        var entryResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:status",
            "--owner-id", project.Id.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=active");

        var snapResult = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", project.Id.ToString(),
            "--description", "Snapshot for activation lifecycle test");

        // Assert
        entryResult.ShouldSucceed();
        snapResult.ShouldSucceed();
    });

    [Fact, Step(2)]
    public Task Step02_CreateActiveClient() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "lifecycle-client");

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(3)]
    public Task Step03_ActiveClientFetchesConfig() => RunStep(3, () =>
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
        configuration["app:status"].ShouldBe("active");

        return Task.CompletedTask;
    });

    [Fact, Step(4)]
    public Task Step04_DeactivateClient() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var clientId = Get<Guid>(ClientIdKey);

        var current = await ApiClient.GetClientHandlerAsync(projectId, clientId, TestCancellationToken);
        current.IsActive.ShouldBeTrue();

        // Act
        GroundControlClient.SetIfMatch(current.Version);
        var updated = await ApiClient.UpdateClientHandlerAsync(
            projectId,
            clientId,
            new UpdateClientRequest { Name = current.Name, IsActive = false },
            TestCancellationToken);

        // Assert
        updated.IsActive.ShouldBeFalse();
    });

    [Fact, Step(5)]
    public Task Step05_DeactivatedClientRejected() => RunStep(5, async () =>
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
            opts.StartupTimeout = TimeSpan.FromSeconds(10);
            opts.ConnectionMode = ConnectionMode.StartupOnly;
            opts.EnableLocalCache = false;
        });

        // Act
        var configuration = configBuilder.Build();

        using var httpClient = Fixture.App.CreateHttpClient("api");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Add("Authorization", $"ApiKey {clientId}:{clientSecret}");
        request.Headers.Add("api-version", "1.0");
        using var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        configuration["app:status"].ShouldBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    });

    [Fact, Step(6)]
    public Task Step06_ReactivateClient() => RunStep(6, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var clientId = Get<Guid>(ClientIdKey);

        var current = await ApiClient.GetClientHandlerAsync(projectId, clientId, TestCancellationToken);
        current.IsActive.ShouldBeFalse();

        // Act
        GroundControlClient.SetIfMatch(current.Version);
        var reactivated = await ApiClient.UpdateClientHandlerAsync(
            projectId,
            clientId,
            new UpdateClientRequest { Name = current.Name, IsActive = true },
            TestCancellationToken);

        // Assert
        reactivated.IsActive.ShouldBeTrue();
    });

    [Fact, Step(7)]
    public Task Step07_ReactivatedClientFetchesConfig() => RunStep(7, () =>
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
        configuration["app:status"].ShouldBe("active");

        return Task.CompletedTask;
    });
}