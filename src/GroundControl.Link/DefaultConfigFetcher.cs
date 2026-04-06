using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GroundControl.Link;

/// <summary>
/// Fetches configuration from the GroundControl REST endpoint with ETag-based conditional requests.
/// </summary>
public sealed partial class DefaultConfigFetcher : IConfigFetcher
{
    private readonly HttpClient _httpClient;
    private readonly GroundControlOptions _options;
    private readonly ILogger<DefaultConfigFetcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConfigFetcher"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the server base address.</param>
    /// <param name="options">The SDK options.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultConfigFetcher(
        HttpClient httpClient,
        GroundControlOptions options,
        ILogger<DefaultConfigFetcher> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<FetchResult> FetchAsync(string? etag, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/config");
        request.Headers.Add("Authorization", $"ApiKey {_options.ClientId}:{_options.ClientSecret}");
        request.Headers.Add("api-version", _options.ApiVersion);

        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{etag}\""));
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            LogNotModified(_logger);
            return new FetchResult { Status = FetchStatus.NotModified, ETag = etag };
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            LogAuthenticationFailed(_logger, (int)response.StatusCode);
            return new FetchResult { Status = FetchStatus.AuthenticationError };
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            LogNotFound(_logger);
            return new FetchResult { Status = FetchStatus.NotFound };
        }

        if (!response.IsSuccessStatusCode)
        {
            LogNonSuccessStatus(_logger, (int)response.StatusCode);
            return new FetchResult { Status = FetchStatus.TransientError };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var config = FlattenJson(json);
        var newEtag = response.Headers.ETag?.Tag?.Trim('"');

        LogFetched(_logger);

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

    [LoggerMessage(1, LogLevel.Debug, "REST fetch: 304 Not Modified.")]
    private static partial void LogNotModified(ILogger logger);

    [LoggerMessage(2, LogLevel.Debug, "REST fetch: configuration loaded.")]
    private static partial void LogFetched(ILogger logger);

    [LoggerMessage(3, LogLevel.Warning, "REST fetch: non-success status code {StatusCode}.")]
    private static partial void LogNonSuccessStatus(ILogger logger, int statusCode);

    [LoggerMessage(4, LogLevel.Error, "REST fetch: authentication failed with status code {StatusCode}.")]
    private static partial void LogAuthenticationFailed(ILogger logger, int statusCode);

    [LoggerMessage(5, LogLevel.Warning, "REST fetch: no active snapshot found (404).")]
    private static partial void LogNotFound(ILogger logger);
}