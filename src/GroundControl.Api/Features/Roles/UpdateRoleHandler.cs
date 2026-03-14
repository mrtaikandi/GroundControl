using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class UpdateRoleHandler : IEndpointHandler
{
    private readonly IRoleStore _store;

    public UpdateRoleHandler(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}", async (
                Guid id,
                UpdateRoleRequest request,
                HttpContext httpContext,
                [FromServices] UpdateRoleHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(id, request, httpContext, cancellationToken))
            .RequireAuthorization(Permissions.RolesWrite)
            .WithContractValidation<UpdateRoleRequest>()
            .WithName(nameof(UpdateRoleHandler));
    }

    private async Task<IResult> HandleAsync(Guid id, UpdateRoleRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions.Clear();
        foreach (var permission in request.Permissions)
        {
            role.Permissions.Add(permission);
        }

        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.UpdatedBy = Guid.Empty;

        var updated = await _store.UpdateAsync(role, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return TypedResults.Problem(detail: "Version conflict.", statusCode: StatusCodes.Status409Conflict);
        }

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(role.Version);
        return TypedResults.Ok(RoleResponse.From(role));
    }
}