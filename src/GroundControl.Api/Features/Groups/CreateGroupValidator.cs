using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Groups;

internal sealed class CreateGroupValidator : IAsyncValidator<CreateGroupRequest>
{
    private readonly IGroupStore _store;

    public CreateGroupValidator(IGroupStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        CreateGroupRequest instance,
        CancellationToken cancellationToken = default)
    {
        var existingGroup = await _store.GetByNameAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        if (existingGroup is not null)
        {
            return [ValidationResult.Error($"A group with name '{instance.Name}' already exists.", [nameof(instance.Name)])];
        }

        return [];
    }
}