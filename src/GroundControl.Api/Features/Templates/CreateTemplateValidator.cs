using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Templates.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Templates;

internal sealed class CreateTemplateValidator : IAsyncValidator<CreateTemplateRequest>
{
    private readonly IGroupStore _groupStore;

    public CreateTemplateValidator(IGroupStore groupStore)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
    }

    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        CreateTemplateRequest instance,
        CancellationToken cancellationToken = default)
    {
        if (instance.GroupId.HasValue)
        {
            var group = await _groupStore.GetByIdAsync(instance.GroupId.Value, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                return [ValidationResult.Error($"Group '{instance.GroupId.Value}' was not found.", [nameof(instance.GroupId)])];
            }
        }

        return [];
    }
}