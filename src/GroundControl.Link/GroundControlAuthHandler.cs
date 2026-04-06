namespace GroundControl.Link;

/// <summary>
/// A delegating handler that adds GroundControl authentication and API version headers
/// to all outgoing requests.
/// </summary>
internal sealed class GroundControlAuthHandler(GroundControlOptions options) : DelegatingHandler
{
    private readonly string _authorization = $"ApiKey {options.ClientId}:{options.ClientSecret}";
    private readonly string _apiVersion = options.ApiVersion;

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("Authorization", _authorization);
        request.Headers.TryAddWithoutValidation("api-version", _apiVersion);
        return base.SendAsync(request, cancellationToken);
    }
}