using System.Diagnostics.Metrics;

namespace GroundControl.Link.Internals;

/// <summary>
/// Metrics instrumentation for the GroundControl Link SDK.
/// </summary>
internal sealed class GroundControlMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _fetchCount;
    private readonly Histogram<double> _fetchDuration;
    private readonly Counter<long> _reloadCount;
    private readonly Counter<long> _sseReconnectCount;
    private readonly UpDownCounter<long> _sseConnected;

    public GroundControlMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("GroundControl.Link");

        _fetchCount = _meter.CreateCounter<long>(
            "groundcontrol.link.fetch.count",
            description: "REST fetch attempts");

        _fetchDuration = _meter.CreateHistogram<double>(
            "groundcontrol.link.fetch.duration",
            unit: "s",
            description: "REST fetch request duration");

        _reloadCount = _meter.CreateCounter<long>(
            "groundcontrol.link.reload.count",
            description: "Configuration reloads triggered");

        _sseReconnectCount = _meter.CreateCounter<long>(
            "groundcontrol.link.sse.reconnect.count",
            description: "SSE reconnection attempts");

        _sseConnected = _meter.CreateUpDownCounter<long>(
            "groundcontrol.link.sse.connected",
            description: "1 when SSE connected, 0 when disconnected");
    }

    public void RecordFetch(string status) => _fetchCount.Add(1, new KeyValuePair<string, object?>("status", status));

    public void RecordFetchDuration(TimeSpan duration) => _fetchDuration.Record(duration.TotalSeconds);

    public void RecordReload(string source) => _reloadCount.Add(1, new KeyValuePair<string, object?>("source", source));

    public void RecordSseReconnect() => _sseReconnectCount.Add(1);

    public void SetSseConnected(bool connected) => _sseConnected.Add(connected ? 1 : -1);

    public void Dispose() => _meter.Dispose();
}