using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GroundControl.Api.Client;

/// <summary>
/// Authenticates requests using a personal access token (PAT).
/// </summary>
public sealed class PatAuthenticationProvider : IAuthenticationProvider
{
    private const string TokenPrefix = "gc_pat_";
    private readonly string _headerValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="token">The personal access token. Must start with <c>gc_pat_</c>.</param>
    public PatAuthenticationProvider(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Token must start with '{TokenPrefix}'.", nameof(token));
        }

        _headerValue = $"Bearer {token}";
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