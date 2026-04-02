using System.Net;

namespace GroundControl.Cli.Tests.Helpers;

internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string PathAndQuery, HttpResponseMessage Response)> _responses = [];

    public FakeHttpHandler RespondTo(HttpMethod method, string pathAndQuery, HttpResponseMessage response)
    {
        _responses.Add((method, pathAndQuery, response));
        return this;
    }

    public FakeHttpHandler RespondTo(HttpMethod method, string pathAndQuery, HttpStatusCode statusCode, string? jsonBody = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
        {
            response.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        return RespondTo(method, pathAndQuery, response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var match = _responses.Find(r =>
            r.Method == request.Method &&
            string.Equals(r.PathAndQuery, request.RequestUri?.PathAndQuery, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match != default
            ? match.Response
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}