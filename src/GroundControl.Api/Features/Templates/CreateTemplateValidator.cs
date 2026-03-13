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

    public async Task<ValidatorResult> ValidateAsync(CreateTemplateRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!instance.GroupId.HasValue)
        {
            return ValidatorResult.Success;
        }

        var group = await _groupStore.GetByIdAsync(instance.GroupId.Value, cancellationToken).ConfigureAwait(false);
        return group is null
            ? ValidatorResult.Fail($"Group '{instance.GroupId.Value}' was not found.", nameof(instance.GroupId))
            : ValidatorResult.Success;
    }
}