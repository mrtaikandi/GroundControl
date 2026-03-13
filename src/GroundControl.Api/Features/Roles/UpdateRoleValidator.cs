using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Extensions.Http;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Roles;

internal sealed class UpdateRoleValidator : IAsyncValidator<UpdateRoleRequest>
{
    private readonly IRoleStore _store;

    public UpdateRoleValidator(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateRoleRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var invalidPermissions = instance.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
        if (invalidPermissions.Count > 0)
        {
            return ValidatorResult.Fail($"Invalid permission(s): {string.Join(", ", invalidPermissions)}.", nameof(instance.Permissions));
        }

        if (!context.HttpContext.Request.RouteValues.TryGetValue<Guid>("id", out var id))
        {
            return ValidatorResult.Problem("Route parameter 'id' is required.", StatusCodes.Status400BadRequest);
        }

        var existingRole = await _store.GetByNameAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        return existingRole is null || existingRole.Id == id
            ? ValidatorResult.Success
            : ValidatorResult.Problem($"A role with name '{instance.Name}' already exists.", StatusCodes.Status409Conflict);
    }
}