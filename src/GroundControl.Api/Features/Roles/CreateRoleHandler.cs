using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Features.Roles;

internal sealed class CreateRoleHandler : IEndpointHandler
{
    private readonly IRoleStore _store;

    public CreateRoleHandler(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static void Endpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(string.Empty, async (
                CreateRoleRequest request,
                [FromServices] CreateRoleHandler handler,
                CancellationToken cancellationToken = default) => await handler.HandleAsync(request, cancellationToken))
            .RequireAuthorization(Permissions.RolesWrite)
            .WithName(nameof(CreateRoleHandler));
    }

    private async Task<IResult> HandleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invalidPermissions = request.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
        if (invalidPermissions.Count > 0)
        {
            return TypedResults.Problem(
                detail: $"Invalid permission(s): {string.Join(", ", invalidPermissions)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var existingRole = await _store.GetByNameAsync(request.Name, cancellationToken).ConfigureAwait(false);
        if (existingRole is not null)
        {
            return TypedResults.Problem(
                detail: $"A role with name '{request.Name}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var role = new Role
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Permissions = [.. request.Permissions],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty,
        };

        await _store.CreateAsync(role, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created($"/api/roles/{role.Id}", RoleResponse.From(role));
    }
}