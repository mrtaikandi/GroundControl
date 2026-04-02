using System.Net.Http.Headers;

namespace GroundControl.Api.Client.Handlers;

/// <summary>
/// A delegating handler that authenticates requests using a personal access token (PAT).
/// </summary>
public sealed class PatAuthenticationHandler : DelegatingHandler
{
    private const string TokenPrefix = "gc_pat_";
    private readonly string _token;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="token">The personal access token. Must start with <c>gc_pat_</c>.</param>
    public PatAuthenticationHandler(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Token must start with '{TokenPrefix}'.", nameof(token));
        }

        _token = token;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return base.SendAsync(request, cancellationToken);
    }
}