using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Shared.Auth;

/// <summary>
/// A delegating handler that injects authentication headers into outgoing HTTP requests
/// based on the configured authentication method.
/// </summary>
internal sealed class AuthenticatingHandler : DelegatingHandler
{
    private readonly AuthOptions _options;

    public AuthenticatingHandler(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_options.Method)
        {
            case null or "":
                break;

            case "Bearer":
                if (string.IsNullOrWhiteSpace(_options.Token))
                {
                    throw new InvalidOperationException(
                        "Authentication method is set to 'Bearer' but no token is configured. " +
                        "Run 'groundcontrol auth login' to configure your credentials.");
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
                break;

            case "ApiKey":
                if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                {
                    throw new InvalidOperationException(
                        "Authentication method is set to 'ApiKey' but client credentials are incomplete. " +
                        "Run 'groundcontrol auth login' to configure your credentials.");
                }

                // Custom ApiKey scheme with colon in value requires TryAddWithoutValidation
                // to bypass AuthenticationHeaderValue parsing.
                request.Headers.TryAddWithoutValidation("Authorization", $"ApiKey {_options.ClientId}:{_options.ClientSecret}");
                break;

            case "Credentials":
                throw new NotSupportedException(
                    "Credentials authentication is not yet supported. " +
                    "Run 'groundcontrol auth login' and choose 'Pat' or 'ApiKey' instead.");

            default:
                throw new InvalidOperationException(
                    $"Unknown authentication method '{_options.Method}'. " +
                    "Supported methods are: Bearer, ApiKey. " +
                    "Run 'groundcontrol auth login' to reconfigure your credentials.");
        }

        return base.SendAsync(request, cancellationToken);
    }
}