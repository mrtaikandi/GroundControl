using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication;

internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = AuthenticationSchemes.ApiKey;

    private readonly IClientStore _clientStore;
    private readonly IValueProtector _protector;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IClientStore clientStore,
        IValueProtector protector)
        : base(options, logger, encoder)
    {
        _clientStore = clientStore;
        _protector = protector;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var credentials = authorization["ApiKey ".Length..];
        var separatorIndex = credentials.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return AuthenticateResult.Fail("Invalid ApiKey format");
        }

        var clientIdPart = credentials[..separatorIndex];
        var providedSecret = credentials[(separatorIndex + 1)..];

        if (!Guid.TryParse(clientIdPart, out var clientId))
        {
            return AuthenticateResult.Fail("Invalid client ID format");
        }

        var client = await _clientStore.GetByIdAsync(clientId, Context.RequestAborted).ConfigureAwait(false);

        // Always perform decrypt + compare even when client is not found to prevent
        // timing oracle that could reveal whether a clientId exists.
        string decryptedSecret;

        try
        {
            decryptedSecret = client is not null ? _protector.Unprotect(client.Secret) : string.Empty;
        }
        catch (CryptographicException)
        {
            decryptedSecret = string.Empty;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);
        var decryptedBytes = Encoding.UTF8.GetBytes(decryptedSecret);
        var secretsMatch = CryptographicOperations.FixedTimeEquals(providedBytes, decryptedBytes);

        if (client is null || !secretsMatch)
        {
            return AuthenticateResult.Fail("Invalid credentials");
        }

        if (!client.IsActive)
        {
            return AuthenticateResult.Fail("Client is deactivated");
        }

        if (client.ExpiresAt.HasValue && client.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("Client credentials have expired");
        }

        // Fire-and-forget: don't let LastUsedAt tracking block authentication
        _clientStore.UpdateLastUsedAsync(clientId, DateTimeOffset.UtcNow).FireAndForget();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, clientId.ToString()),
            new("projectId", client.ProjectId.ToString()),
        };

        foreach (var scope in client.Scopes)
        {
            claims.Add(new Claim("clientScope", $"{scope.Key}:{scope.Value}"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}