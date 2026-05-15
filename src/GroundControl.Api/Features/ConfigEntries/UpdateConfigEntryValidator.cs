using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Security.Protection;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryValidator : IAsyncValidator<UpdateConfigEntryRequest>
{
    private readonly ConfigEntryValidation _validation;

    public UpdateConfigEntryValidator(ConfigEntryValidation validation)
    {
        _validation = validation ?? throw new ArgumentNullException(nameof(validation));
    }

    public Task<ValidatorResult> ValidateAsync(UpdateConfigEntryRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (!_validation.IsValidKey(instance.Key))
        {
            return Task.FromResult(ValidatorResult.Fail(ConfigEntryValidation.KeyPatternErrorMessage, nameof(instance.Key)));
        }

        if (!_validation.IsValidValueType(instance.ValueType))
        {
            return Task.FromResult(ValidatorResult.Fail($"ValueType '{instance.ValueType}' is not supported.", nameof(instance.ValueType)));
        }

        var result = new ValidatorResult();
        foreach (var scopedValue in instance.Values)
        {
            if (instance.IsSensitive && SensitiveSourceValueProtector.IsMaskSentinel(scopedValue.Value))
            {
                result.AddError(
                    "Sensitive values cannot be set to the mask sentinel '***'. Submit the actual secret or omit the value.",
                    nameof(instance.Values));
                continue;
            }

            var valueError = _validation.ValidateValue(scopedValue.Value, instance.ValueType);
            if (valueError is not null)
            {
                result.AddError(valueError, nameof(instance.Values));
            }
        }

        return Task.FromResult(result.IsFailed ? result : ValidatorResult.Success);
    }
}