using System.Diagnostics.CodeAnalysis;
using System.Net.ServerSentEvents;

namespace GroundControl.Link;

/// <summary>
/// An SSE client that connects to the GroundControl server and streams configuration events.
/// Uses <see cref="SseParser"/> for W3C-compliant parsing including empty <c>id:</c> handling,
/// multi-line <c>data:</c> concatenation, and <c>retry:</c> field support.
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
        request.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (_lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", _lastEventId);
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, heartbeatCts.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content
            .ReadAsStreamAsync(heartbeatCts.Token).ConfigureAwait(false);

        var parser = SseParser.Create(stream);

        await foreach (var item in parser.EnumerateAsync(heartbeatCts.Token).ConfigureAwait(false))
        {
            heartbeatCts.CancelAfter(_options.SseHeartbeatTimeout);

            _lastEventId = string.IsNullOrEmpty(parser.LastEventId) ? null : parser.LastEventId;

            LogEventReceived(_logger, item.EventType, _lastEventId);

            yield return new SseEvent
            {
                EventType = item.EventType,
                Data = item.Data,
                Id = _lastEventId,
            };
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

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handler is owned and disposed by HttpClient")]
    private static HttpClient CreateHttpClient(GroundControlOptions options)
    {
        var handler = new GroundControlAuthHandler(options) { InnerHandler = new HttpClientHandler() };
        return new HttpClient(handler) { BaseAddress = new Uri(options.ServerUrl) };
    }

    [LoggerMessage(1, LogLevel.Debug, "SSE event received: type={EventType}, id={EventId}.")]
    private static partial void LogEventReceived(ILogger logger, string eventType, string? eventId);
}