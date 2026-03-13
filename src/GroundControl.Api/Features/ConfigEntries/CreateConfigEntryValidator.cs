using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class CreateConfigEntryValidator : IAsyncValidator<CreateConfigEntryRequest>
{
    private readonly IScopeStore _scopeStore;

    public CreateConfigEntryValidator(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(
        CreateConfigEntryRequest instance,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        List<ValidationResult> errors = [];

        if (!ConfigEntryValidation.IsValidValueType(instance.ValueType))
        {
            errors.Add(ValidationResult.Error(
                $"ValueType '{instance.ValueType}' is not supported.",
                [nameof(instance.ValueType)]));
            return ValidatorResult.ValidationProblem(errors);
        }

        foreach (var scopedValue in instance.Values)
        {
            var valueError = ConfigEntryValidation.ValidateValue(scopedValue.Value, instance.ValueType);
            if (valueError is not null)
            {
                errors.Add(ValidationResult.Error(valueError, [nameof(instance.Values)]));
            }
        }

        if (errors.Count > 0)
        {
            return ValidatorResult.ValidationProblem(errors);
        }

        var scopeError = await ConfigEntryValidation.ValidateScopesAsync(instance.Values, _scopeStore, cancellationToken).ConfigureAwait(false);
        if (scopeError is not null)
        {
            errors.Add(ValidationResult.Error(scopeError, [nameof(instance.Values)]));
            return ValidatorResult.ValidationProblem(errors);
        }

        return ValidatorResult.Success;
    }
}