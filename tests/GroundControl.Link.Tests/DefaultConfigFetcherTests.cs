using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

namespace GroundControl.Link.Tests;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
public sealed class DefaultConfigFetcherTests : IDisposable
{
    private readonly MockHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly DefaultConfigFetcher _fetcher;

    private static readonly GroundControlOptions TestOptions = new()
    {
        ServerUrl = "http://localhost",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        ApiVersion = "1.0"
    };

    public DefaultConfigFetcherTests()
    {
        _handler = new MockHandler();
        var authHandler = new GroundControlAuthHandler(TestOptions) { InnerHandler = _handler };
        _httpClient = new HttpClient(authHandler) { BaseAddress = new Uri("http://localhost") };
        _fetcher = new DefaultConfigFetcher(_httpClient, TestOptions, NullLogger<DefaultConfigFetcher>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task FetchAsync_200_ReturnsFlatDictionaryAndETag()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"Key1": "Value1", "Key2": "Value2"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.Success);
        result.Config.ShouldNotBeNull();
        result.Config.Count.ShouldBe(2);
        result.Config["Key1"].ShouldBe("Value1");
        result.Config["Key2"].ShouldBe("Value2");
        result.ETag.ShouldBe("v1");
    }

    [Fact]
    public async Task FetchAsync_304_ReturnsNotModified()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotModified);

        // Act
        var result = await _fetcher.FetchAsync("v1", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.NotModified);
        result.ETag.ShouldBe("v1");
    }

    [Fact]
    public async Task FetchAsync_SendsIfNoneMatchHeader_WhenETagProvided()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotModified);

        // Act
        await _fetcher.FetchAsync("v42", TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.IfNoneMatch.ShouldContain(h => h.Tag == "\"v42\"");
    }

    [Fact]
    public async Task FetchAsync_DoesNotSendIfNoneMatch_WhenETagIsNull()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.IfNoneMatch.ShouldBeEmpty();
    }

    [Fact]
    public async Task FetchAsync_SendsApiVersionHeader()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.TryGetValues("api-version", out var values).ShouldBeTrue();
        values.ShouldContain("1.0");
    }

    [Fact]
    public async Task FetchAsync_SendsAuthorizationHeader()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.TryGetValues("Authorization", out var values).ShouldBeTrue();
        values.ShouldContain("ApiKey test-client:test-secret");
    }

    [Fact]
    public async Task FetchAsync_NonSuccessStatusCode_ReturnsTransientError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.TransientError);
    }

    [Fact]
    public async Task FetchAsync_Unauthorized_ReturnsAuthenticationError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.Unauthorized);

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.AuthenticationError);
    }

    [Fact]
    public async Task FetchAsync_Forbidden_ReturnsAuthenticationError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.Forbidden);

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.AuthenticationError);
    }

    [Fact]
    public async Task FetchAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotFound);

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.NotFound);
    }

    [Fact]
    public async Task FetchAsync_ETagFromResponseHeaderIsReturned()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"Key": "Val"}, "snapshotVersion": 3}""", "\"snapshot-3\"");

        // Act
        var result = await _fetcher.FetchAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ETag.ShouldBe("snapshot-3");
    }

    [Fact]
    public void FlattenJson_NestedObject_ProducesColonSeparatedKeys()
    {
        // Arrange
        var json = """{"data": {"Db": {"Host": "localhost", "Port": "5432"}}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(2);
        result["Db:Host"].ShouldBe("localhost");
        result["Db:Port"].ShouldBe("5432");
    }

    [Fact]
    public void FlattenJson_DeeplyNestedObject_ProducesMultiLevelKeys()
    {
        // Arrange
        var json = """{"data": {"A": {"B": {"C": "deep"}}}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(1);
        result["A:B:C"].ShouldBe("deep");
    }

    [Fact]
    public void FlattenJson_ArrayElements_UseNumericIndex()
    {
        // Arrange
        var json = """{"data": {"Hosts": ["alpha", "beta", "gamma"]}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(3);
        result["Hosts:0"].ShouldBe("alpha");
        result["Hosts:1"].ShouldBe("beta");
        result["Hosts:2"].ShouldBe("gamma");
    }

    [Fact]
    public void FlattenJson_ArrayOfObjects_UsesNumericIndexWithPropertyName()
    {
        // Arrange
        var json = """{"data": {"Servers": [{"Host": "a"}, {"Host": "b"}]}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(2);
        result["Servers:0:Host"].ShouldBe("a");
        result["Servers:1:Host"].ShouldBe("b");
    }

    [Fact]
    public void FlattenJson_NullValues_AreOmitted()
    {
        // Arrange
        var json = """{"data": {"Present": "yes", "Missing": null, "Also": "here"}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContainKey("Present");
        result.ShouldContainKey("Also");
        result.ShouldNotContainKey("Missing");
    }

    [Fact]
    public void FlattenJson_BooleanAndNumericValues_ConvertedToString()
    {
        // Arrange
        var json = """{"data": {"Enabled": true, "Count": 42, "Rate": 3.14}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(3);
        result["Enabled"].ShouldBe("True");
        result["Count"].ShouldBe("42");
        result["Rate"].ShouldBe("3.14");
    }

    [Fact]
    public void FlattenJson_EmptyDataObject_ReturnsEmptyDictionary()
    {
        // Arrange
        var json = """{"data": {}, "snapshotVersion": 1}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FlattenJson_MissingDataProperty_ReturnsEmptyDictionary()
    {
        // Arrange
        var json = """{"snapshotVersion": 1}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FlattenJson_FlatKeyValues_ReturnedDirectly()
    {
        // Arrange
        var json = """{"data": {"Simple": "value", "Another": "one"}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result.Count.ShouldBe(2);
        result["Simple"].ShouldBe("value");
        result["Another"].ShouldBe("one");
    }

    [Fact]
    public void FlattenJson_KeysAreCaseInsensitive()
    {
        // Arrange
        var json = """{"data": {"MyKey": "value"}}""";

        // Act
        var result = DefaultConfigFetcher.FlattenJson(json);

        // Assert
        result["mykey"].ShouldBe("value");
        result["MYKEY"].ShouldBe("value");
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string? _content;
        private string? _etag;

        public HttpRequestMessage? LastRequest { get; private set; }

        public void SetResponse(HttpStatusCode statusCode, string? content = null, string? etag = null)
        {
            _statusCode = statusCode;
            _content = content;
            _etag = etag;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(_statusCode);

            if (_content is not null)
            {
                response.Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json");
            }

            if (_etag is not null)
            {
                response.Headers.ETag = new EntityTagHeaderValue(_etag);
            }

            return Task.FromResult(response);
        }
    }
}