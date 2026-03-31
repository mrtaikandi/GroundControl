using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Audit;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class UpdateRoleHandler : IEndpointHandler
{
    private readonly IRoleStore _store;
    private readonly AuditRecorder _audit;

    public UpdateRoleHandler(IRoleStore store, AuditRecorder audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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

        if (!EntityTagHeaders.TryParseIfMatch(httpContext, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var oldName = role.Name;
        var oldDescription = role.Description;
        var oldPermissions = role.Permissions.ToList();

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

        List<FieldChange> changes = [
            .. AuditRecorder.CompareFields("Name", oldName, role.Name),
            .. AuditRecorder.CompareFields("Description", oldDescription, role.Description),
            .. AuditRecorder.CompareCollections("Permissions", oldPermissions, role.Permissions.ToList()),
        ];

        await _audit.RecordAsync("Role", role.Id, null, "Updated", changes, cancellationToken: cancellationToken).ConfigureAwait(false);

        httpContext.Response.Headers.ETag = EntityTagHeaders.Format(role.Version);
        return TypedResults.Ok(RoleResponse.From(role));
    }
}