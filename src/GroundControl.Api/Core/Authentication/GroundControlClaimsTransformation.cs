using System.Security.Claims;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authentication;

namespace GroundControl.Api.Core.Authentication;

/// <summary>
/// Loads the domain user and validates the account is active.
/// Returns an empty principal for inactive or unknown users, causing a 401 response.
/// </summary>
internal sealed partial class GroundControlClaimsTransformation : IClaimsTransformation
{
    private readonly IUserStore _userStore;
    private readonly ILogger<GroundControlClaimsTransformation> _logger;

    /// <summary>
    /// Loads the domain user and validates the account is active.
    /// Returns an empty principal for inactive or unknown users, causing a 401 response.
    /// </summary>
    public GroundControlClaimsTransformation(ILogger<GroundControlClaimsTransformation> logger, IUserStore userStore)
    {
        _userStore = userStore;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // API key-authenticated principals are clients, not users — skip user validation
        if (principal.Identity is ClaimsIdentity { AuthenticationType: ApiKeyAuthenticationHandler.SchemeName })
        {
            return principal;
        }

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            return principal;
        }

        // NoAuth mode uses Guid.Empty — skip DB lookup
        if (userId == Guid.Empty)
        {
            return principal;
        }

        var user = await _userStore.GetByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(_logger, userId);
            return new ClaimsPrincipal();
        }

        if (!user.IsActive)
        {
            LogInactiveUser(_logger, userId);
            return new ClaimsPrincipal();
        }

        return principal;
    }

    [LoggerMessage(1, LogLevel.Warning, "Claims transformation failed: user {UserId} not found.")]
    private static partial void LogUserNotFound(ILogger<GroundControlClaimsTransformation> logger, Guid userId);

    [LoggerMessage(2, LogLevel.Warning, "Claims transformation rejected inactive user {UserId}.")]
    private static partial void LogInactiveUser(ILogger<GroundControlClaimsTransformation> logger, Guid userId);
}