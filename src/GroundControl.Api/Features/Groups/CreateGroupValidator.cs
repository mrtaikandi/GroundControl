using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

namespace GroundControl.Api.Features.Groups;

internal sealed class CreateGroupValidator : IAsyncValidator<CreateGroupRequest>
{
    private readonly IGroupStore _store;

    public CreateGroupValidator(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ValidatorResult> ValidateAsync(
        CreateGroupRequest instance,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var existingGroup = await _store.GetByNameAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        if (existingGroup is not null)
        {
            return ValidatorResult.ValidationProblem(ValidationResult.Error($"A group with name '{instance.Name}' already exists.", [nameof(instance.Name)]));
        }

        return ValidatorResult.Success;
    }
}