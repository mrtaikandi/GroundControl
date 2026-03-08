using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class GetRoleHandler : IEndpointHandler
{
    private readonly IRoleStore _store;

    public GetRoleHandler(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] GetRoleHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.RolesRead)
            .WithName(nameof(GetRoleHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var role = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return TypedResults.Problem(detail: $"Role '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(role.Version);
        return TypedResults.Ok(RoleResponse.From(role));
    }
}