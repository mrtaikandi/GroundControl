using System.Net;
using System.Net.Http;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using GroundControl.Link;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying expired clients are rejected with 401 and Link
/// SDK handles the failure gracefully.
/// </summary>
public sealed class ClientExpirationRejection : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";

    public ClientExpirationRejection(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_SetupProjectAndSnapshot() => RunStep(1, async () =>
    {
        // Arrange & Act
        var projectResult = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "Client Expiration Project");
        projectResult.ShouldSucceed();
        var project = projectResult.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);

        var entryResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:feature-flag",
            "--owner-id", project.Id.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=enabled");

        var snapResult = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", project.Id.ToString(),
            "--description", "Snapshot for expiration test");

        // Assert
        entryResult.ShouldSucceed();
        snapResult.ShouldSucceed();
    });

    [Fact, Step(2)]
    public Task Step02_CreateExpiredClient() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var expiredTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToString("o");

        // Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", projectId.ToString(),
            "--name", "expired-client",
            "--expires-at", expiredTimestamp);

        // Assert
        result.ShouldSucceed();
        var client = result.ParseOutput<CreateClientResponse>();
        client.Id.ShouldNotBe(Guid.Empty);
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(3)]
    public Task Step03_LinkSdkHandlesExpiredGracefully() => RunStep(3, () =>
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

        // Assert
        configuration["app:feature-flag"].ShouldBeNull();

        return Task.CompletedTask;
    });

    [Fact, Step(4)]
    public Task Step04_RawHttpReturns401() => RunStep(4, async () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);

        using var httpClient = Fixture.App.CreateHttpClient("api");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Add("Authorization", $"ApiKey {clientId}:{clientSecret}");
        request.Headers.Add("api-version", "1.0");

        // Act
        using var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    });

    [Fact, Step(5)]
    public Task Step05_ClientRecordStillExists() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var clientId = Get<Guid>(ClientIdKey);

        // Act
        var apiClient = await ApiClient.GetClientHandlerAsync(projectId, clientId, TestCancellationToken);

        // Assert
        apiClient.Id.ShouldBe(clientId);
        apiClient.Name.ShouldBe("expired-client");
        apiClient.ExpiresAt.ShouldNotBeNull();
        apiClient.ExpiresAt.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);
    });
}