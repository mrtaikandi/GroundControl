using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Shared.Security.Authorization;

/// <summary>
/// An authorization requirement that demands a specific permission.
/// </summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;