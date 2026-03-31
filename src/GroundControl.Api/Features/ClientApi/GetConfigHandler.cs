using System.Security.Claims;
using GroundControl.Api.Features.ClientApi.Contracts;
using GroundControl.Api.Shared.Resolvers;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace GroundControl.Api.Features.ClientApi;

internal sealed class GetConfigHandler : IEndpointHandler
{
    private readonly SnapshotCache _cache;
    private readonly IScopeResolver _scopeResolver;
    private readonly IValueProtector _protector;

    public GetConfigHandler(SnapshotCache cache, IScopeResolver scopeResolver, IValueProtector protector)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/config", async (
                HttpContext httpContext,
                [FromServices] GetConfigHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(httpContext, cancellationToken))
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AuthenticationSchemes.ApiKey })
            .WithName(nameof(GetConfigHandler));
    }

    private async Task<IResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var projectIdClaim = httpContext.User.FindFirstValue("projectId");
        if (projectIdClaim is null || !Guid.TryParse(projectIdClaim, out var projectId))
        {
            return TypedResults.Problem(
                detail: "Missing or invalid projectId claim.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var clientScopes = ParseClientScopes(httpContext.User);

        var snapshot = await _cache.GetOrLoadAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return TypedResults.Problem(
                detail: $"No active snapshot found for project '{projectId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // ETag / If-None-Match support
        var etag = $"\"{snapshot.SnapshotVersion}\"";
        if (httpContext.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch) &&
            string.Equals(ifNoneMatch.ToString(), etag, StringComparison.Ordinal))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        var data = new Dictionary<string, string>();
        foreach (var entry in snapshot.Entries)
        {
            var resolved = _scopeResolver.Resolve((IReadOnlyList<ScopedValue>)entry.Values, clientScopes);
            if (resolved is null)
            {
                continue;
            }

            var value = entry.IsSensitive ? _protector.Unprotect(resolved.Value) : resolved.Value;
            data[entry.Key] = value;
        }

        httpContext.Response.Headers[HeaderNames.ETag] = etag;

        return TypedResults.Ok(new ClientConfigResponse
        {
            Data = data,
            SnapshotId = snapshot.Id,
            SnapshotVersion = snapshot.SnapshotVersion,
        });
    }

    private static Dictionary<string, string> ParseClientScopes(ClaimsPrincipal principal)
    {
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var claim in principal.FindAll("clientScope"))
        {
            var separatorIndex = claim.Value.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                var key = claim.Value[..separatorIndex];
                var value = claim.Value[(separatorIndex + 1)..];
                scopes[key] = value;
            }
        }

        return scopes;
    }
}