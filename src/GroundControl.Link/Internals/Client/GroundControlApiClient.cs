using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;

namespace GroundControl.Link.Internals.Client;

/// <summary>
/// Default implementation of <see cref="IGroundControlApiClient"/>.
/// Handles REST config fetching with status mapping and SSE stream connection.
/// </summary>
internal sealed class GroundControlApiClient : IGroundControlApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroundControlApiClient> _logger;

    public GroundControlApiClient(HttpClient httpClient, ILogger<GroundControlApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FetchResult> FetchConfigAsync(string? etag, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");

        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{etag}\""));
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotModified:
                _logger.LogNotModified();
                return new FetchResult { Status = FetchStatus.NotModified, ETag = etag };

            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                _logger.LogAuthenticationFailed((int)response.StatusCode);
                return new FetchResult { Status = FetchStatus.AuthenticationError };

            case HttpStatusCode.NotFound:
                _logger.LogNotFound();
                return new FetchResult { Status = FetchStatus.NotFound };
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogNonSuccessStatus((int)response.StatusCode);
            return new FetchResult { Status = FetchStatus.TransientError };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var config = FlattenJson(json);
        var newEtag = response.Headers.ETag?.Tag.Trim('"');

        _logger.LogFetched(newEtag);

        return new FetchResult
        {
            Status = FetchStatus.Success,
            Config = config,
            ETag = newEtag
        };
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetConfigStreamAsync(string? lastEventId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config/stream");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Text.EventStream));

        if (lastEventId is not null)
        {
            request.Headers.Add(HeaderNames.LastEventId, lastEventId);
        }

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    internal static Dictionary<string, string> FlattenJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            FlattenElement(data, string.Empty, result);
        }

        return result;
    }

    internal static void FlattenElement(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prefix.Length > 0 ? $"{prefix}:{prop.Name}" : prop.Name;
                    FlattenElement(prop.Value, key, result);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{prefix}:{index++}", result);
                }

                break;

            case JsonValueKind.Null:
                break;

            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            default:
                result[prefix] = element.ToString();
                break;
        }
    }
}

internal static partial class GroundControlApiClientLogs
{
    [LoggerMessage(1, LogLevel.Debug, "REST fetch: 304 Not Modified.")]
    public static partial void LogNotModified(this ILogger<GroundControlApiClient> logger);

    [LoggerMessage(2, LogLevel.Debug, "REST fetch: configuration loaded. E-Tag: {etag}")]
    public static partial void LogFetched(this ILogger<GroundControlApiClient> logger, string? etag);

    [LoggerMessage(3, LogLevel.Warning, "REST fetch: non-success status code {StatusCode}.")]
    public static partial void LogNonSuccessStatus(this ILogger<GroundControlApiClient> logger, int statusCode);

    [LoggerMessage(4, LogLevel.Error, "REST fetch: authentication failed with status code {StatusCode}.")]
    public static partial void LogAuthenticationFailed(this ILogger<GroundControlApiClient> logger, int statusCode);

    [LoggerMessage(5, LogLevel.Warning, "REST fetch: no active snapshot found (404).")]
    public static partial void LogNotFound(this ILogger<GroundControlApiClient> logger);
}