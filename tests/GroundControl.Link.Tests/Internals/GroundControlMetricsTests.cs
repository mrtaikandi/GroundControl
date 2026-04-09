using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace GroundControl.Link.Tests.Internals;

public sealed class GroundControlMetricsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;
    private readonly GroundControlMetrics _metrics;

    public GroundControlMetricsTests()
    {
        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        _metrics = new GroundControlMetrics(_meterFactory);
    }

    public void Dispose()
    {
        _metrics.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void RecordFetch_IncrementsCounter()
    {
        // Arrange
        using var collector = new MetricCollector<long>(_meterFactory, "GroundControl.Link", "groundcontrol.link.fetch.count");

        // Act
        _metrics.RecordFetch("success");

        // Assert
        var measurement = collector.GetMeasurementSnapshot().EvaluateAsCounter();
        measurement.ShouldBe(1);
    }

    [Fact]
    public void RecordFetchDuration_RecordsHistogram()
    {
        // Arrange
        using var collector = new MetricCollector<double>(_meterFactory, "GroundControl.Link", "groundcontrol.link.fetch.duration");

        // Act
        _metrics.RecordFetchDuration(TimeSpan.FromMilliseconds(500));

        // Assert
        collector.GetMeasurementSnapshot().Count.ShouldBe(1);
    }

    [Fact]
    public void RecordReload_IncrementsCounter()
    {
        // Arrange
        using var collector = new MetricCollector<long>(_meterFactory, "GroundControl.Link", "groundcontrol.link.reload.count");

        // Act
        _metrics.RecordReload("sse");

        // Assert
        var measurement = collector.GetMeasurementSnapshot().EvaluateAsCounter();
        measurement.ShouldBe(1);
    }

    [Fact]
    public void SetSseConnected_SetsUpDownCounter()
    {
        // Arrange
        using var collector = new MetricCollector<long>(_meterFactory, "GroundControl.Link", "groundcontrol.link.sse.connected");

        // Act
        _metrics.SetSseConnected(true);
        _metrics.SetSseConnected(false);

        // Assert
        var measurements = collector.GetMeasurementSnapshot();
        measurements.Count.ShouldBe(2);
    }
}