using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class DeleteRoleHandler : IEndpointHandler
{
    private readonly IRoleStore _store;

    public DeleteRoleHandler(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] DeleteRoleHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.RolesWrite)
            .WithName(nameof(DeleteRoleHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var role = await _store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return TypedResults.Problem(detail: $"Role '{id}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion))
        {
            return TypedResults.Problem(detail: "If-Match header is required.", statusCode: StatusCodes.Status428PreconditionRequired);
        }

        var isReferenced = await _store.IsReferencedByUsersAsync(id, cancellationToken).ConfigureAwait(false);
        if (isReferenced)
        {
            return TypedResults.Problem(
                detail: $"Role '{role.Name}' cannot be deleted because it is referenced by one or more users.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var deleted = await _store.DeleteAsync(id, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }
}