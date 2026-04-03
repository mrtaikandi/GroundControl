using System.Net;

namespace GroundControl.Api.Client.Tests.Infrastructure;

internal sealed class ResponseCapturingHandler : DelegatingHandler
{
    public HttpResponseMessage? LastResponse { get; private set; }

    /// <summary>
    /// Gets the HTTP status code of the last response.
    /// </summary>
    public HttpStatusCode? LastStatusCode { get; private set; }

    /// <summary>
    /// Gets the buffered response body bytes, captured before NSwag disposes the response.
    /// </summary>
    public byte[]? LastResponseBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        LastStatusCode = response.StatusCode;

        // Buffer the response body so tests can read it after NSwag disposes the response.
        // Replace the content with a replayable ByteArrayContent so NSwag can also read it.
        if (response.Content is not null)
        {
            LastResponseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var bufferedContent = new ByteArrayContent(LastResponseBody);
            foreach (var header in response.Content.Headers)
            {
                bufferedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = bufferedContent;
        }
        else
        {
            LastResponseBody = null;
        }

        LastResponse = response;
        return LastResponse;
    }
}