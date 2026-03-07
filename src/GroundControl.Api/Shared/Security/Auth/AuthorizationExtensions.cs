using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Shared.Security.Auth;

internal static class AuthorizationExtensions
{
    /// <summary>
    /// Registers one authorization policy per permission string, each requiring a claim of the given type with the permission value.
    /// </summary>
    public static AuthorizationBuilder AddPolicies(this AuthorizationBuilder builder, IReadOnlySet<string> permissions, string claimType)
    {
        foreach (var permission in permissions)
        {
            builder.AddPolicy(permission, policy => policy.RequireClaim(claimType, permission));
        }

        return builder;
    }
}