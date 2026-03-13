using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Roles.Contracts;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

namespace GroundControl.Api.Features.Roles;

internal sealed class CreateRoleValidator : IAsyncValidator<CreateRoleRequest>
{
    private readonly IRoleStore _store;

    public CreateRoleValidator(IRoleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(CreateRoleRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var invalidPermissions = instance.Permissions.Where(p => !Permissions.All.Contains(p)).ToList();
        if (invalidPermissions.Count > 0)
        {
            return ValidatorResult.ValidationProblem(ValidationResult.Error($"Invalid permission(s): {string.Join(", ", invalidPermissions)}.", [nameof(instance.Permissions)]));
        }

        var existingRole = await _store.GetByNameAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        return existingRole is null
            ? ValidatorResult.Success
            : ValidatorResult.ValidationProblem(ValidationResult.Error($"A role with name '{instance.Name}' already exists.", [nameof(instance.Name)]));
    }
}