namespace GroundControl.Api.Client.Handlers;

/// <summary>
/// A delegating handler that authenticates requests using the ApiKey scheme with client credentials.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : DelegatingHandler
{
    private readonly string _headerValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    public ApiKeyAuthenticationHandler(Guid clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        _headerValue = $"ApiKey {clientId}:{clientSecret}";
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Custom ApiKey scheme with colon in value requires TryAddWithoutValidation
        // to bypass AuthenticationHeaderValue parsing.
        request.Headers.TryAddWithoutValidation("Authorization", _headerValue);
        return base.SendAsync(request, cancellationToken);
    }
}