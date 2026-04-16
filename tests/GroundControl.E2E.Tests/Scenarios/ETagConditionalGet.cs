using System.Net;
using System.Net.Http;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying ETag/If-None-Match conditional delivery on the
/// client config endpoint.
/// </summary>
public sealed class ETagConditionalGet : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string ConfigEntryIdKey = "ConfigEntryId";
    private const string ConfigEntryVersionKey = "ConfigEntryVersion";
    private const string ClientIdKey = "ClientId";
    private const string ClientSecretKey = "ClientSecret";
    private const string ETagKey = "ETag";

    public ETagConditionalGet(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_SetupProjectSnapshotAndClient() => RunStep(1, async () =>
    {
        // Arrange & Act
        var projectResult = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "ETag Conditional GET Project");
        projectResult.ShouldSucceed();
        var project = projectResult.ParseOutput<ProjectResponse>();
        Set(ProjectIdKey, project.Id);

        var entryResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "create",
            "--key", "app:setting",
            "--owner-id", project.Id.ToString(),
            "--owner-type", "Project",
            "--value-type", "String",
            "--value", "default=original-value");
        entryResult.ShouldSucceed();
        var entry = entryResult.ParseOutput<ConfigEntryResponse>();
        Set(ConfigEntryIdKey, entry.Id);
        Set(ConfigEntryVersionKey, entry.Version);

        var snapResult = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", project.Id.ToString(),
            "--description", "Initial snapshot for ETag test");
        snapResult.ShouldSucceed();

        var clientResult = await Cli.RunAsync(TestCancellationToken,
            "client", "create",
            "--project-id", project.Id.ToString(),
            "--name", "etag-test-client");
        clientResult.ShouldSucceed();
        var client = clientResult.ParseOutput<CreateClientResponse>();

        // Assert
        client.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        Set(ClientIdKey, client.Id);
        Set(ClientSecretKey, client.ClientSecret);
    });

    [Fact, Step(2)]
    public Task Step02_CaptureETag() => RunStep(2, async () =>
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.Tag.ShouldNotBeNullOrWhiteSpace();

        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);
        body.ShouldContain("original-value");

        Set(ETagKey, response.Headers.ETag.Tag);
    });

    [Fact, Step(3)]
    public Task Step03_ConditionalGetReturns304() => RunStep(3, async () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);
        var etag = Get<string>(ETagKey);

        using var httpClient = Fixture.App.CreateHttpClient("api");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Add("Authorization", $"ApiKey {clientId}:{clientSecret}");
        request.Headers.Add("api-version", "1.0");
        request.Headers.Add("If-None-Match", etag);

        // Act
        using var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);

        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);
        body.ShouldBeNullOrWhiteSpace();
    });

    [Fact, Step(4)]
    public Task Step04_UpdateEntryAndRepublish() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var entryId = Get<Guid>(ConfigEntryIdKey);
        var version = Get<long>(ConfigEntryVersionKey);

        // Act
        var updateResult = await Cli.RunAsync(TestCancellationToken,
            "config-entry", "update",
            entryId.ToString(),
            "--version", version.ToString(),
            "--value-type", "String",
            "--value", "default=updated-value");
        updateResult.ShouldSucceed();

        var snapResult = await Cli.RunAsync(TestCancellationToken,
            "snapshot", "publish",
            "--project-id", projectId.ToString(),
            "--description", "Updated snapshot for ETag test");

        // Assert
        snapResult.ShouldSucceed();
    });

    [Fact, Step(5)]
    public Task Step05_ConditionalGetReturnsFreshData() => RunStep(5, async () =>
    {
        // Arrange
        var clientId = Get<Guid>(ClientIdKey);
        var clientSecret = Get<string>(ClientSecretKey);
        var oldEtag = Get<string>(ETagKey);

        using var httpClient = Fixture.App.CreateHttpClient("api");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Add("Authorization", $"ApiKey {clientId}:{clientSecret}");
        request.Headers.Add("api-version", "1.0");
        request.Headers.Add("If-None-Match", oldEtag);

        // Act
        using var response = await httpClient.SendAsync(request, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestCancellationToken);
        body.ShouldContain("updated-value");
        body.ShouldNotContain("original-value");

        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.Tag.ShouldNotBe(oldEtag);
    });
}