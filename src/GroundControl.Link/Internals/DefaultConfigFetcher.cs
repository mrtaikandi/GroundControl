using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GroundControl.Link.Internals;

/// <summary>
/// Fetches configuration from the GroundControl REST endpoint with ETag-based conditional requests.
/// </summary>
internal sealed class DefaultConfigFetcher : IConfigFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultConfigFetcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConfigFetcher"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the server base address.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultConfigFetcher(HttpClient httpClient, ILogger<DefaultConfigFetcher> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GroundControlApiEndpoints.ClientConfig);

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

    private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, string> result)
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

            default:
                result[prefix] = element.ToString();
                break;
        }
    }
}

internal static partial class DefaultConfigFetcherLogs
{
    [LoggerMessage(1, LogLevel.Debug, "REST fetch: 304 Not Modified.")]
    public static partial void LogNotModified(this ILogger<DefaultConfigFetcher> logger);

    [LoggerMessage(2, LogLevel.Debug, "REST fetch: configuration loaded. E-Tag: {etag}")]
    public static partial void LogFetched(this ILogger<DefaultConfigFetcher> logger, string? etag);

    [LoggerMessage(3, LogLevel.Warning, "REST fetch: non-success status code {StatusCode}.")]
    public static partial void LogNonSuccessStatus(this ILogger<DefaultConfigFetcher> logger, int statusCode);

    [LoggerMessage(4, LogLevel.Error, "REST fetch: authentication failed with status code {StatusCode}.")]
    public static partial void LogAuthenticationFailed(this ILogger<DefaultConfigFetcher> logger, int statusCode);

    [LoggerMessage(5, LogLevel.Warning, "REST fetch: no active snapshot found (404).")]
    public static partial void LogNotFound(this ILogger<DefaultConfigFetcher> logger);
}