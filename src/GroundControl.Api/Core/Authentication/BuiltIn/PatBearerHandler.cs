using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.Authentication.BuiltIn;

internal sealed partial class PatBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PatBearer";
    private const string TokenPrefix = "gc_pat_";
    private const string PatPermissionsClaimType = "pat_permissions";

    private readonly IPersonalAccessTokenStore _tokenStore;
    private readonly IUserStore _userStore;

    public PatBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IPersonalAccessTokenStore tokenStore,
        IUserStore userStore)
        : base(options, logger, encoder)
    {
        _tokenStore = tokenStore;
        _userStore = userStore;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToHexStringLower(hashBytes);

        var pat = await _tokenStore.GetByTokenHashAsync(tokenHash, Context.RequestAborted).ConfigureAwait(false);
        if (pat is null)
        {
            return AuthenticateResult.Fail("Invalid personal access token.");
        }

        if (pat.IsRevoked)
        {
            LogRevokedTokenUsed(Logger, pat.Id, pat.UserId);
            return AuthenticateResult.Fail("Personal access token has been revoked.");
        }

        if (pat.ExpiresAt.HasValue && pat.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            LogExpiredTokenUsed(Logger, pat.Id, pat.UserId);
            return AuthenticateResult.Fail("Personal access token has expired.");
        }

        var user = await _userStore.GetByIdAsync(pat.UserId, Context.RequestAborted).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return AuthenticateResult.Fail("Token owner account is inactive or not found.");
        }

        // Fire-and-forget: don't let LastUsedAt tracking block authentication
        _ = Task.Run(() => _tokenStore.UpdateLastUsedAsync(pat.Id, DateTimeOffset.UtcNow));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, pat.UserId.ToString()),
        };

        if (pat.Permissions is { Count: > 0 })
        {
            foreach (var permission in pat.Permissions)
            {
                claims.Add(new Claim(PatPermissionsClaimType, permission));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        LogPatAuthenticated(Logger, pat.Id, pat.UserId);

        return AuthenticateResult.Success(ticket);
    }

    [LoggerMessage(1, LogLevel.Information, "PAT {TokenId} authenticated user {UserId}.")]
    private static partial void LogPatAuthenticated(ILogger logger, Guid tokenId, Guid userId);

    [LoggerMessage(2, LogLevel.Warning, "Revoked PAT {TokenId} used by user {UserId}.")]
    private static partial void LogRevokedTokenUsed(ILogger logger, Guid tokenId, Guid userId);

    [LoggerMessage(3, LogLevel.Warning, "Expired PAT {TokenId} used by user {UserId}.")]
    private static partial void LogExpiredTokenUsed(ILogger logger, Guid tokenId, Guid userId);
}