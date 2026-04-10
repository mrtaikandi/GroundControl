using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

namespace GroundControl.Link.Tests;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
public sealed class GroundControlApiClientTests : IDisposable
{
    private readonly MockHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly GroundControlApiClient _client;

    private static readonly GroundControlOptions TestOptions = new()
    {
        ServerUrl = new Uri("http://localhost"),
        ClientId = "test-client",
        ClientSecret = "test-secret",
        ApiVersion = "1.0"
    };

    public GroundControlApiClientTests()
    {
        _handler = new MockHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost") };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", $"{TestOptions.ClientId}:{TestOptions.ClientSecret}");
        _httpClient.DefaultRequestHeaders.Add(HeaderNames.ApiVersion, TestOptions.ApiVersion);
        _client = new GroundControlApiClient(_httpClient, NullLogger<GroundControlApiClient>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task FetchConfigAsync_200_ReturnsFlatDictionaryAndETag()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"Key1": "Value1", "Key2": "Value2"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

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
    public async Task FetchConfigAsync_304_ReturnsNotModified()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotModified);

        // Act
        var result = await _client.FetchConfigAsync("v1", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.NotModified);
        result.ETag.ShouldBe("v1");
    }

    [Fact]
    public async Task FetchConfigAsync_SendsIfNoneMatchHeader_WhenETagProvided()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotModified);

        // Act
        await _client.FetchConfigAsync("v42", TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.IfNoneMatch.ShouldContain(h => h.Tag == "\"v42\"");
    }

    [Fact]
    public async Task FetchConfigAsync_DoesNotSendIfNoneMatch_WhenETagIsNull()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.IfNoneMatch.ShouldBeEmpty();
    }

    [Fact]
    public async Task FetchConfigAsync_SendsApiVersionHeader()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.TryGetValues("api-version", out var values).ShouldBeTrue();
        values.ShouldContain("1.0");
    }

    [Fact]
    public async Task FetchConfigAsync_SendsAuthorizationHeader()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"A": "1"}, "snapshotVersion": 1}""", "\"v1\"");

        // Act
        await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest.Headers.TryGetValues("Authorization", out var values).ShouldBeTrue();
        values.ShouldContain("ApiKey test-client:test-secret");
    }

    [Fact]
    public async Task FetchConfigAsync_NonSuccessStatusCode_ReturnsTransientError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.TransientError);
    }

    [Fact]
    public async Task FetchConfigAsync_Unauthorized_ReturnsAuthenticationError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.Unauthorized);

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.AuthenticationError);
    }

    [Fact]
    public async Task FetchConfigAsync_Forbidden_ReturnsAuthenticationError()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.Forbidden);

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.AuthenticationError);
    }

    [Fact]
    public async Task FetchConfigAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.NotFound);

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(FetchStatus.NotFound);
    }

    [Fact]
    public async Task FetchConfigAsync_ETagFromResponseHeaderIsReturned()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, """{"data": {"Key": "Val"}, "snapshotVersion": 3}""", "\"snapshot-3\"");

        // Act
        var result = await _client.FetchConfigAsync(null, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ETag.ShouldBe("snapshot-3");
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