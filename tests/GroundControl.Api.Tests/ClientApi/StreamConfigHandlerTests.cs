using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Features.ClientApi.Contracts;
using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots.Contracts;
using GroundControl.Persistence.Contracts;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.ClientApi;

[Collection("MongoDB")]
public sealed class StreamConfigHandlerTests : ApiHandlerTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public StreamConfigHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Stream_HappyPath_SendsInitialConfigEvent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "app.name", project.Id, value: "MyApp");
        await CreateConfigEntryAsync(adminClient, "app.version", project.Id, value: "2.0.0");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "stream-client");

        // Act
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        using var request = CreateStreamRequest(client);
        using var response = await adminClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");

        await using var reader = await CreateSseReaderAsync(response, cts.Token);
        var firstEvent = await reader.ReadNextEventAsync(cts.Token);
        firstEvent.ShouldNotBeNull();
        firstEvent.EventType.ShouldBe("config");
        firstEvent.Id.ShouldNotBeNullOrWhiteSpace();

        var config = JsonSerializer.Deserialize<ClientConfigResponse>(firstEvent.Data, JsonOptions);
        config.ShouldNotBeNull();
        config.Data.ShouldContainKeyAndValue("app.name", "MyApp");
        config.Data.ShouldContainKeyAndValue("app.version", "2.0.0");
        config.SnapshotVersion.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Stream_SnapshotPublished_SendsUpdatedConfigEvent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "app.name", project.Id, value: "InitialValue");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "update-client");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        using var request = CreateStreamRequest(client);
        using var response = await adminClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var reader = await CreateSseReaderAsync(response, cts.Token);

        var initialEvent = await reader.ReadNextEventAsync(cts.Token);
        initialEvent.ShouldNotBeNull();
        initialEvent.EventType.ShouldBe("config");

        // Act — add a new config entry and publish another snapshot
        await CreateConfigEntryAsync(adminClient, "app.new-key", project.Id, value: "NewValue");
        await PublishSnapshotAsync(adminClient, project.Id);

        // Assert
        var updateEvent = await reader.ReadNextEventAsync(cts.Token);
        updateEvent.ShouldNotBeNull();
        updateEvent.EventType.ShouldBe("config");

        var config = JsonSerializer.Deserialize<ClientConfigResponse>(updateEvent.Data, JsonOptions);
        config.ShouldNotBeNull();
        config.Data.ShouldContainKeyAndValue("app.name", "InitialValue");
        config.Data.ShouldContainKeyAndValue("app.new-key", "NewValue");
    }

    [Fact]
    public async Task Stream_HeartbeatInterval_SendsHeartbeatEvent()
    {
        // Arrange
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ClientApi:HeartbeatIntervalSeconds"] = "1",
        });
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "key1", project.Id, value: "val1");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "heartbeat-client");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        using var request = CreateStreamRequest(client);
        using var response = await adminClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var reader = await CreateSseReaderAsync(response, cts.Token);

        var initialEvent = await reader.ReadNextEventAsync(cts.Token);
        initialEvent.ShouldNotBeNull();
        initialEvent.EventType.ShouldBe("config");

        // Act
        var heartbeatEvent = await reader.ReadNextEventAsync(cts.Token);

        // Assert
        heartbeatEvent.ShouldNotBeNull();
        heartbeatEvent.EventType.ShouldBe("heartbeat");
        heartbeatEvent.Data.ShouldContain("timestamp");
    }

    [Fact]
    public async Task Stream_ReconnectWithCurrentLastEventId_SkipsInitialConfig()
    {
        // Arrange
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ClientApi:HeartbeatIntervalSeconds"] = "1",
        });
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        await CreateConfigEntryAsync(adminClient, "key1", project.Id, value: "val1");
        await PublishSnapshotAsync(adminClient, project.Id);

        var client = await CreateApiClientAsync(adminClient, project.Id, "reconnect-client");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // First connection to get the snapshot ID
        using var firstRequest = CreateStreamRequest(client);
        using var firstResponse = await adminClient.SendAsync(firstRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await using var firstReader = await CreateSseReaderAsync(firstResponse, cts.Token);

        var firstEvent = await firstReader.ReadNextEventAsync(cts.Token);
        firstEvent.ShouldNotBeNull();
        var snapshotId = firstEvent.Id;
        snapshotId.ShouldNotBeNullOrWhiteSpace();

        // Act — reconnect with Last-Event-ID set to current snapshot ID
        using var reconnectRequest = CreateStreamRequest(client, lastEventId: snapshotId);
        using var reconnectResponse = await adminClient.SendAsync(reconnectRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        reconnectResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var reconnectReader = await CreateSseReaderAsync(reconnectResponse, cts.Token);

        // Assert — the first event should be heartbeat (no duplicate config), or a config event from a new snapshot
        // Use heartbeat to prove we skipped the initial config push
        var nextEvent = await reconnectReader.ReadNextEventAsync(cts.Token);
        nextEvent.ShouldNotBeNull();
        nextEvent.EventType.ShouldBe("heartbeat");
    }

    [Fact]
    public async Task Stream_WithoutAuth_Returns401()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();

        // Act
        var response = await httpClient.GetAsync("/client/config/stream", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stream_NoActiveSnapshot_SendsNoInitialConfigEvent()
    {
        // Arrange
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ClientApi:HeartbeatIntervalSeconds"] = "1",
        });
        using var adminClient = factory.CreateClient();

        var project = await CreateProjectAsync(adminClient);
        var client = await CreateApiClientAsync(adminClient, project.Id, "no-snapshot-client");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // Act
        using var request = CreateStreamRequest(client);
        using var response = await adminClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var reader = await CreateSseReaderAsync(response, cts.Token);

        // Assert — first event should be a heartbeat, not config
        var firstEvent = await reader.ReadNextEventAsync(cts.Token);
        firstEvent.ShouldNotBeNull();
        firstEvent.EventType.ShouldBe("heartbeat");
    }

    #region SSE Reader

    private sealed record SseEvent
    {
        public string? EventType { get; init; }

        public string? Id { get; init; }

        public string Data { get; init; } = string.Empty;
    }

    private sealed class SseStreamReader : IAsyncDisposable
    {
        private readonly StreamReader _reader;

        public SseStreamReader(StreamReader reader)
        {
            _reader = reader;
        }

        public async Task<SseEvent?> ReadNextEventAsync(CancellationToken cancellationToken)
        {
            string? eventType = null;
            string? id = null;
            var data = string.Empty;
            var hasData = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);

                if (line is null)
                {
                    return null;
                }

                if (line.Length == 0)
                {
                    if (hasData || eventType is not null)
                    {
                        return new SseEvent
                        {
                            EventType = eventType,
                            Id = id,
                            Data = data,
                        };
                    }

                    continue;
                }

                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    eventType = line["event: ".Length..];
                }
                else if (line.StartsWith("id: ", StringComparison.Ordinal))
                {
                    id = line["id: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data = line["data: ".Length..];
                    hasData = true;
                }
            }

            return null;
        }

        public ValueTask DisposeAsync()
        {
            _reader.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<SseStreamReader> CreateSseReaderAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var reader = new StreamReader(stream, leaveOpen: true);
        return new SseStreamReader(reader);
    }

    #endregion

    #region Test Helpers

    private static HttpRequestMessage CreateStreamRequest(CreateClientResponse client, string? lastEventId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/client/config/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", $"{client.Id}:{client.ClientSecret}");

        if (lastEventId is not null)
        {
            request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
        }

        return request;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient httpClient)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
        };

        var response = await httpClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task<CreateClientResponse> CreateApiClientAsync(HttpClient httpClient, Guid projectId, string name)
    {
        var request = new CreateClientRequest { Name = name };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/clients", request, WebJsonSerializerOptions, TestCancellationToken);

        response.EnsureSuccessStatusCode();

        var client = await response.Content.ReadFromJsonAsync<CreateClientResponse>(WebJsonSerializerOptions, TestCancellationToken);
        client.ShouldNotBeNull();

        return client;
    }

    private static async Task PublishSnapshotAsync(HttpClient httpClient, Guid projectId)
    {
        var request = new PublishSnapshotRequest { Description = "Test snapshot" };

        var response = await httpClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/snapshots", request, WebJsonSerializerOptions, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient httpClient,
        string key,
        Guid projectId,
        string value = "default",
        bool isSensitive = false)
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = projectId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
            IsSensitive = isSensitive,
        };

        var response = await httpClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion
}