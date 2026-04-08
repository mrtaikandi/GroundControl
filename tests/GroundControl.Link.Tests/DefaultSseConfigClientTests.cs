using System.Net;
using System.Text;
using GroundControl.Link.Internals;
using Microsoft.Extensions.Options;

namespace GroundControl.Link.Tests;

public sealed class DefaultSseConfigClientTests : IAsyncDisposable
{
    private FakeHandler? _handler;
    private HttpClient? _httpClient;
    private DefaultSseConfigClient? _sut;

    public ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task StreamAsync_ParsesConfigEventWithAllFields()
    {
        // Arrange
        var sseText = "event: config\nid: 42\ndata: {\"entries\":[]}\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe("config");
        events[0].Data.ShouldBe("{\"entries\":[]}");
        events[0].Id.ShouldBe("42");
    }

    [Fact]
    public async Task StreamAsync_ParsesHeartbeatEvent()
    {
        // Arrange
        var sseText = "event: heartbeat\ndata: {\"timestamp\":\"2026-03-27T00:00:00Z\"}\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe("heartbeat");
        events[0].Data.ShouldBe("{\"timestamp\":\"2026-03-27T00:00:00Z\"}");
        events[0].Id.ShouldBeNull();
    }

    [Fact]
    public async Task StreamAsync_ParsesEventWithoutId()
    {
        // Arrange
        var sseText = "event: config\ndata: {\"entries\":[]}\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].Id.ShouldBeNull();
    }

    [Fact]
    public async Task StreamAsync_DefaultsToMessageEventType()
    {
        // Arrange — no event: line, just data
        var sseText = "data: hello\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe("message");
        events[0].Data.ShouldBe("hello");
    }

    [Fact]
    public async Task StreamAsync_BlankLineTriggersDispatch()
    {
        // Arrange — two events separated by blank lines
        var sseText = "event: config\ndata: first\nid: 1\n\nevent: config\ndata: second\nid: 2\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(2);
        events[0].Data.ShouldBe("first");
        events[1].Data.ShouldBe("second");
    }

    [Fact]
    public async Task StreamAsync_MultiLineDataConcatenated()
    {
        // Arrange — multiple data: lines for one event
        var sseText = "event: config\ndata: line1\ndata: line2\ndata: line3\nid: 1\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].Data.ShouldBe("line1\nline2\nline3");
    }

    [Fact]
    public async Task StreamAsync_ParsesFieldWithoutSpaceAfterColon()
    {
        // Arrange — no space after colon (lenient parsing per W3C spec)
        var sseText = "event:config\ndata:{\"entries\":[]}\nid:99\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe("config");
        events[0].Data.ShouldBe("{\"entries\":[]}");
        events[0].Id.ShouldBe("99");
    }

    [Fact]
    public async Task StreamAsync_IgnoresCommentLines()
    {
        // Arrange — comment lines start with ':'
        var sseText = ": this is a comment\nevent: config\ndata: payload\nid: 1\n\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].Data.ShouldBe("payload");
    }

    [Fact]
    public async Task StreamAsync_HeartbeatTimeout_ThrowsOperationCanceledException()
    {
        // Arrange — stream blocks after first event, no heartbeat resets the timer
        var sseText = "event: config\ndata: {}\nid: 1\n\n";
        CreateClient(sseText, heartbeatTimeout: TimeSpan.FromMilliseconds(200), blockAfterData: true);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _sut!.StreamAsync()) { }
        });
    }

    [Fact]
    public async Task StreamAsync_SendsLastEventIdOnReconnect()
    {
        // Arrange — first stream yields event with id, second stream verifies header
        var handler = new FakeHandler();
        handler.Enqueue("event: config\ndata: {}\nid: snapshot-42\n\n");
        handler.Enqueue("event: config\ndata: {}\nid: snapshot-43\n\n");

        _handler = handler;
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var gcHttpClient = new GroundControlApiClient(_httpClient, NullLogger<GroundControlApiClient>.Instance);
        _sut = new DefaultSseConfigClient(gcHttpClient, CreateOptions(), NullLogger<DefaultSseConfigClient>.Instance);

        // Act — first stream
        await foreach (var _ in _sut.StreamAsync(TestContext.Current.CancellationToken)) { }

        // Act — second stream (reconnect)
        await foreach (var _ in _sut.StreamAsync(TestContext.Current.CancellationToken)) { }

        // Assert
        handler.CapturedLastEventIds.Count.ShouldBe(2);
        handler.CapturedLastEventIds[0].ShouldBeNull();
        handler.CapturedLastEventIds[1].ShouldBe("snapshot-42");
    }

    [Fact]
    public async Task StreamAsync_PartialReadsParseCorrectly()
    {
        // Arrange — stream returns data one byte at a time
        var sseText = "event: config\ndata: chunked\nid: 1\n\n";
        var handler = new FakeHandler();
        handler.EnqueueChunked(sseText, chunkSize: 1);

        _handler = handler;
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var gcHttpClient = new GroundControlApiClient(_httpClient, NullLogger<GroundControlApiClient>.Instance);
        _sut = new DefaultSseConfigClient(gcHttpClient, CreateOptions(), NullLogger<DefaultSseConfigClient>.Instance);

        // Act
        var events = await CollectEventsAsync();

        // Assert
        events.Count.ShouldBe(1);
        events[0].EventType.ShouldBe("config");
        events[0].Data.ShouldBe("chunked");
        events[0].Id.ShouldBe("1");
    }

    [Fact]
    public async Task StreamAsync_DoesNotDispatchWithoutBlankLine()
    {
        // Arrange — event fields present but no trailing blank line (stream ends abruptly)
        var sseText = "event: config\ndata: incomplete\nid: 1\n";
        CreateClient(sseText);

        // Act
        var events = await CollectEventsAsync();

        // Assert — no event dispatched because blank line never arrived
        events.ShouldBeEmpty();
    }

    private void CreateClient(
        string sseText,
        TimeSpan? heartbeatTimeout = null,
        bool blockAfterData = false)
    {
        var handler = new FakeHandler();

        if (blockAfterData)
        {
            handler.EnqueueBlocking(sseText);
        }
        else
        {
            handler.Enqueue(sseText);
        }

        _handler = handler;
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var options = CreateOptions();
        if (heartbeatTimeout.HasValue)
        {
            options.Value.SseHeartbeatTimeout = heartbeatTimeout.Value;
        }

        var gcHttpClient = new GroundControlApiClient(_httpClient, NullLogger<GroundControlApiClient>.Instance);
        _sut = new DefaultSseConfigClient(gcHttpClient, options, NullLogger<DefaultSseConfigClient>.Instance);
    }

    private async Task<List<SseEvent>> CollectEventsAsync()
    {
        List<SseEvent> events = [];

        await foreach (var sseEvent in _sut!.StreamAsync(TestContext.Current.CancellationToken))
        {
            events.Add(sseEvent);
        }

        return events;
    }

    private static IOptions<GroundControlOptions> CreateOptions() => Options.Create(new GroundControlOptions
    {
        ServerUrl = new Uri("http://localhost"),
        ClientId = "test-client",
        ClientSecret = "test-secret",
        SseHeartbeatTimeout = TimeSpan.FromMinutes(2),
    });

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<Stream>> _responseFactories = new();

        public List<string?> CapturedLastEventIds { get; } = [];

        public void Enqueue(string sseText)
        {
            var data = Encoding.UTF8.GetBytes(sseText);
            _responseFactories.Enqueue(() => new MemoryStream(data));
        }

        public void EnqueueBlocking(string sseText)
        {
            var data = Encoding.UTF8.GetBytes(sseText);
            _responseFactories.Enqueue(() => new BlockingStream(data));
        }

        public void EnqueueChunked(string sseText, int chunkSize)
        {
            var data = Encoding.UTF8.GetBytes(sseText);
            _responseFactories.Enqueue(() => new ChunkedStream(data, chunkSize));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedLastEventIds.Add(
                request.Headers.TryGetValues("Last-Event-ID", out var values)
                    ? values.First()
                    : null);

            var stream = _responseFactories.Dequeue()();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream),
            };

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// A stream that returns initial data, then blocks indefinitely on subsequent reads
    /// until the cancellation token is triggered.
    /// </summary>
    private sealed class BlockingStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        public BlockingStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(count, _data.Length - _position);
            if (available > 0)
            {
                Array.Copy(_data, _position, buffer, offset, available);
                _position += available;
            }

            return available;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var available = Math.Min(buffer.Length, _data.Length - _position);
            if (available > 0)
            {
                _data.AsSpan(_position, available).CopyTo(buffer.Span);
                _position += available;
                return available;
            }

            // Block until cancelled (simulates a stale connection with no data)
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A stream that returns data in small chunks to simulate partial network reads.
    /// </summary>
    private sealed class ChunkedStream : Stream
    {
        private readonly byte[] _data;
        private int _position;
        private readonly int _chunkSize;

        public ChunkedStream(byte[] data, int chunkSize)
        {
            _data = data;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(Math.Min(count, _chunkSize), _data.Length - _position);
            if (available > 0)
            {
                Array.Copy(_data, _position, buffer, offset, available);
                _position += available;
            }

            return available;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var available = Math.Min(Math.Min(buffer.Length, _chunkSize), _data.Length - _position);
            if (available > 0)
            {
                _data.AsSpan(_position, available).CopyTo(buffer.Span);
                _position += available;
            }

            return ValueTask.FromResult(available);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}