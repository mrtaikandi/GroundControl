using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GroundControl.Api.Client;

/// <summary>
/// Authenticates requests using the ApiKey scheme with client credentials.
/// </summary>
public sealed class ApiKeyAuthenticationProvider : IAuthenticationProvider
{
    private readonly string _headerValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    public ApiKeyAuthenticationProvider(Guid clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        _headerValue = $"ApiKey {clientId}:{clientSecret}";
    }

    /// <inheritdoc />
    public Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Add("Authorization", _headerValue);
        return Task.CompletedTask;
    }
}