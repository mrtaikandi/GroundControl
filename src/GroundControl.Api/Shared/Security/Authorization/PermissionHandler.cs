using System.Security.Claims;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Shared.Security.Authorization;

/// <summary>
/// Resolves effective permissions from user grants and role permission bundles.
/// </summary>
internal sealed partial class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserStore _userStore;
    private readonly IRoleStore _roleStore;
    private readonly ILogger<PermissionHandler> _logger;

    /// <summary>
    /// Resolves effective permissions from user grants and role permission bundles.
    /// </summary>
    public PermissionHandler(ILogger<PermissionHandler> logger, IUserStore userStore, IRoleStore roleStore)
    {
        _userStore = userStore;
        _roleStore = roleStore;
        _logger = logger;
    }

    private const string PatPermissionsClaimType = "pat_permissions";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            return;
        }

        // NoAuth mode uses Guid.Empty as a synthetic admin — succeed on everything
        if (userId == Guid.Empty)
        {
            context.Succeed(requirement);
            return;
        }

        var user = await _userStore.GetByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            LogUserNotFound(_logger, userId, requirement.Permission);
            return;
        }

        if (!user.IsActive)
        {
            LogPermissionDenied(_logger, userId, requirement.Permission);
            context.Fail(new AuthorizationFailureReason(this, "User account is inactive."));
            return;
        }

        // Collect effective permissions from all grants
        var effectivePermissions = new HashSet<string>();
        foreach (var grant in user.Grants)
        {
            var role = await _roleStore.GetByIdAsync(grant.RoleId).ConfigureAwait(false);
            if (role is null)
            {
                continue;
            }

            foreach (var permission in role.Permissions)
            {
                effectivePermissions.Add(permission);
            }
        }

        // PAT scoping: intersect with token's declared permissions if present
        var patPermissions = context.User.FindAll(PatPermissionsClaimType).Select(c => c.Value).ToHashSet();
        if (patPermissions.Count > 0)
        {
            effectivePermissions.IntersectWith(patPermissions);
        }

        if (effectivePermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            LogPermissionDenied(_logger, userId, requirement.Permission);
        }
    }

    [LoggerMessage(1, LogLevel.Warning, "User {UserId} not found during permission check for {Permission}.")]
    private static partial void LogUserNotFound(ILogger<PermissionHandler> logger, Guid userId, string permission);

    [LoggerMessage(2, LogLevel.Warning, "User {UserId} denied permission {Permission}.")]
    private static partial void LogPermissionDenied(ILogger<PermissionHandler> logger, Guid userId, string permission);
}