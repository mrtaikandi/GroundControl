using System.Net.ServerSentEvents;
using Microsoft.Extensions.Options;

namespace GroundControl.Link.Internals;

/// <summary>
/// An SSE client that connects to the GroundControl server and streams configuration events.
/// Uses <see cref="SseParser"/> for W3C-compliant parsing including empty <c>id:</c> handling,
/// multi-line <c>data:</c> concatenation, and <c>retry:</c> field support.
/// </summary>
internal sealed partial class GroundControlSseClient : IGroundControlSseClient
{
    private readonly IGroundControlApiClient _apiClient;
    private readonly GroundControlOptions _options;
    private readonly ILogger<GroundControlSseClient> _logger;

    /// <inheritdoc />
    public string? LastEventId { get; set; }

    public GroundControlSseClient(IGroundControlApiClient apiClient, IOptions<GroundControlOptions> options, ILogger<GroundControlSseClient> logger)
    {
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SseEvent> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatCts.CancelAfter(_options.SseHeartbeatTimeout);

        using var response = await _apiClient.GetConfigStreamAsync(LastEventId, heartbeatCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(heartbeatCts.Token).ConfigureAwait(false);
        var parser = SseParser.Create(stream);

        await foreach (var item in parser.EnumerateAsync(heartbeatCts.Token).ConfigureAwait(false))
        {
            heartbeatCts.CancelAfter(_options.SseHeartbeatTimeout);

            LastEventId = string.IsNullOrEmpty(parser.LastEventId) ? null : parser.LastEventId;
            LogEventReceived(_logger, item.EventType, LastEventId);

            yield return new SseEvent
            {
                EventType = item.EventType,
                Data = item.Data,
                Id = LastEventId,
            };
        }
    }

    [LoggerMessage(1, LogLevel.Debug, "SSE event received: type={EventType}, id={EventId}.")]
    private static partial void LogEventReceived(ILogger logger, string eventType, string? eventId);
}