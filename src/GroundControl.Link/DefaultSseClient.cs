namespace GroundControl.Link;

/// <summary>
/// An SSE client that connects to the GroundControl server and streams configuration events.
/// Parses the text/event-stream format, monitors heartbeat timeouts, and tracks Last-Event-ID
/// for reconnection.
/// </summary>
public sealed partial class DefaultSseClient : ISseClient
{
    private readonly HttpClient _httpClient;
    private readonly GroundControlOptions _options;
    private readonly ILogger<DefaultSseClient> _logger;
    private readonly bool _ownsHttpClient;
    private string? _lastEventId;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSseClient"/> class
    /// that creates and owns its own <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="options">The SDK options.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultSseClient(GroundControlOptions options, ILogger<DefaultSseClient> logger)
        : this(CreateHttpClient(options), options, logger, ownsHttpClient: true)
    {
    }

    internal DefaultSseClient(HttpClient httpClient, GroundControlOptions options, ILogger<DefaultSseClient> logger)
        : this(httpClient, options, logger, ownsHttpClient: false)
    {
    }

    private DefaultSseClient(
        HttpClient httpClient,
        GroundControlOptions options,
        ILogger<DefaultSseClient> logger,
        bool ownsHttpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsHttpClient = ownsHttpClient;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SseEvent> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatCts.CancelAfter(_options.SseHeartbeatTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config/stream");
        request.Headers.Add("Authorization", $"ApiKey {_options.ClientId}:{_options.ClientSecret}");
        request.Headers.Add("api-version", _options.ApiVersion);

        if (_lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", _lastEventId);
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, heartbeatCts.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(heartbeatCts.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        // Log heartbeat timeout as a warning when the linked CTS fires from the timer (not external cancellation)
        heartbeatCts.Token.Register(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                LogHeartbeatTimeout(_logger);
            }
        });

        string? eventType = null;
        string? data = null;
        string? id = null;

        while (await reader.ReadLineAsync(heartbeatCts.Token).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data is not null)
                {
                    var sseEvent = new SseEvent
                    {
                        EventType = eventType ?? "message",
                        Data = data,
                        Id = id,
                    };

                    LogEventReceived(_logger, sseEvent.EventType, sseEvent.Id);

                    // Any event (config or heartbeat) proves the connection is alive
                    heartbeatCts.CancelAfter(_options.SseHeartbeatTimeout);

                    yield return sseEvent;
                }

                eventType = null;
                data = null;
                id = null;
                continue;
            }

            // W3C SSE spec: lines starting with U+003A COLON are comments
            if (line[0] == ':')
            {
                continue;
            }

            var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
            string fieldName;
            string fieldValue;

            if (colonIndex >= 0)
            {
                fieldName = line[..colonIndex];

                // Per W3C SSE spec: strip one leading space after the colon if present
                fieldValue = colonIndex + 1 < line.Length && line[colonIndex + 1] == ' '
                    ? line[(colonIndex + 2)..]
                    : line[(colonIndex + 1)..];
            }
            else
            {
                fieldName = line;
                fieldValue = string.Empty;
            }

            switch (fieldName)
            {
                case "event":
                    eventType = fieldValue;
                    break;
                case "data":
                    data = data is null ? fieldValue : $"{data}\n{fieldValue}";
                    break;
                case "id":
                    id = fieldValue;
                    _lastEventId = fieldValue;
                    break;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static HttpClient CreateHttpClient(GroundControlOptions options) =>
        new() { BaseAddress = new Uri(options.ServerUrl) };

    [LoggerMessage(1, LogLevel.Debug, "SSE event received: type={EventType}, id={EventId}.")]
    private static partial void LogEventReceived(ILogger logger, string eventType, string? eventId);

    [LoggerMessage(2, LogLevel.Warning, "SSE heartbeat timeout — no heartbeat received within the configured timeout.")]
    private static partial void LogHeartbeatTimeout(ILogger logger);
}