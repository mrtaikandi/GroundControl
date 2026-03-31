using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryValidator : IAsyncValidator<UpdateConfigEntryRequest>
{
    private readonly IScopeStore _scopeStore;

    public UpdateConfigEntryValidator(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateConfigEntryRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!ConfigEntryValidation.IsValidValueType(instance.ValueType))
        {
            return ValidatorResult.Fail($"ValueType '{instance.ValueType}' is not supported.", nameof(instance.ValueType));
        }

        var result = new ValidatorResult();
        foreach (var scopedValue in instance.Values)
        {
            var valueError = ConfigEntryValidation.ValidateValue(scopedValue.Value, instance.ValueType);
            if (valueError is not null)
            {
                result.AddError(valueError, nameof(instance.Values));
            }
        }

        if (result.IsFailed)
        {
            return result;
        }

        var scopeError = await ConfigEntryValidation.ValidateScopesAsync(instance.Values, _scopeStore, cancellationToken).ConfigureAwait(false);
        return scopeError is null ? ValidatorResult.Success : result.AddError(scopeError, nameof(instance.Values));
    }
}