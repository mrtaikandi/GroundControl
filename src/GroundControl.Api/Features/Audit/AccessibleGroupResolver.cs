using System.Security.Claims;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Audit;

internal static class AccessibleGroupResolver
{
    /// <summary>
    /// Resolves the group IDs the caller can access from their grants.
    /// Returns <c>null</c> when the caller has a system-wide grant (no filter applied).
    /// Returns an empty list when the caller cannot be identified or has no grants.
    /// </summary>
    public static async Task<IReadOnlyList<Guid?>?> GetAccessibleGroupIdsAsync(
        IUserStore userStore,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userStore);
        ArgumentNullException.ThrowIfNull(principal);

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            return [];
        }

        // NoAuth mode uses Guid.Empty as a synthetic admin — grant full access
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await userStore.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return [];
        }

        // A grant with Resource == null is system-wide — caller sees all groups
        if (user.Grants.Any(g => g.Resource is null))
        {
            return null;
        }

        return user.Grants.Select(g => g.Resource).Distinct().ToList();
    }
}