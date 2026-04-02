namespace GroundControl.Cli.Shared.ApiClient;

internal sealed class ApiVersionHandler : DelegatingHandler
{
    private const string HeaderName = "api-version";
    private const string HeaderValue = "1.0";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(HeaderName, HeaderValue);
        return base.SendAsync(request, cancellationToken);
    }
}