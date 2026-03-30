using Microsoft.AspNetCore.Authorization;

namespace GroundControl.Api.Core.Authentication;

/// <summary>
/// An authorization requirement that demands a specific permission.
/// </summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;