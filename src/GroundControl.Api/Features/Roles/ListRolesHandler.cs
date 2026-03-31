using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class ListRolesHandler : IEndpointHandler
{
    private readonly IRoleStore _store;

    public ListRolesHandler(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(string.Empty, async (
                [FromServices] ListRolesHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(cancellationToken))
            .RequireAuthorization(Permissions.RolesRead)
            .WithName(nameof(ListRolesHandler));
    }

    private async Task<IResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _store.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(roles.Select(RoleResponse.From).ToList());
    }
}