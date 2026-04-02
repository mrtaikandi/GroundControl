namespace GroundControl.Api.Client.Handlers;

/// <summary>
/// A delegating handler that ensures an API version header is present on outgoing requests.
/// </summary>
public sealed class ApiVersionHandler : DelegatingHandler
{
    /// <summary>
    /// The name of the API version header.
    /// </summary>
    private const string HeaderName = "api-version";

    /// <summary>
    /// The default API version value to apply when the header is missing.
    /// </summary>
    private const string HeaderValue = "1.0";

    /// <summary>
    /// Adds the default API version header to the request when it has not already been specified.
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The asynchronous operation that forwards the request to the next handler.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, HeaderValue);
        }

        return base.SendAsync(request, cancellationToken);
    }
}