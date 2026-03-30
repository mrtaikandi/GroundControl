using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Core.Authentication;

internal static class AuthorizationExtensions
{
    /// <summary>
    /// Registers one authorization policy per permission string, each backed by a <see cref="PermissionRequirement"/>.
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder, IReadOnlySet<string> permissions)
    {
        foreach (var permission in permissions)
        {
            builder.AddPolicy(permission, policy => policy.AddRequirements(new PermissionRequirement(permission)));
        }

        return builder;
    }
}