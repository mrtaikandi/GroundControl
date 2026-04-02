namespace GroundControl.Api.Client.Tests.Infrastructure;

internal sealed class ResponseCapturingHandler : DelegatingHandler
{
    public HttpResponseMessage? LastResponse { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastResponse = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return LastResponse;
    }
}