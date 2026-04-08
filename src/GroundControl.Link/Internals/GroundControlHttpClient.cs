using System.Net.Http.Headers;
using System.Net.Mime;

namespace GroundControl.Link.Internals;

/// <summary>
/// Typed HTTP client for the GroundControl API.
/// Encapsulates endpoint paths and request construction for both REST and SSE consumers.
/// </summary>
internal sealed class GroundControlHttpClient
{
    private readonly HttpClient _httpClient;

    public GroundControlHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends a conditional GET request to the configuration endpoint.
    /// </summary>
    /// <param name="etag">An optional ETag for conditional requests (If-None-Match).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The HTTP response message. The caller owns and must dispose this response.</returns>
    public async Task<HttpResponseMessage> GetConfigAsync(string? etag, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GroundControlApiEndpoints.ClientConfig);

        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{etag}\""));
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a streaming SSE connection to the configuration stream endpoint.
    /// </summary>
    /// <param name="lastEventId">An optional Last-Event-ID for resuming from a prior position.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The HTTP response message with headers-only completion. The caller owns and must dispose this response.</returns>
    public async Task<HttpResponseMessage> GetConfigStreamAsync(string? lastEventId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GroundControlApiEndpoints.ClientConfigStream);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Text.EventStream));

        if (lastEventId is not null)
        {
            request.Headers.Add(HeaderNames.LastEventId, lastEventId);
        }

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }
}