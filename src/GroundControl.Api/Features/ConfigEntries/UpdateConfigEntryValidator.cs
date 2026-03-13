using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.ConfigEntries;

internal sealed class UpdateConfigEntryValidator : IAsyncValidator<UpdateConfigEntryRequest>
{
    private readonly IScopeStore _scopeStore;

    public UpdateConfigEntryValidator(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(UpdateConfigEntryRequest instance, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationResult>();

        if (!ConfigEntryValidation.IsValidValueType(instance.ValueType))
        {
            errors.Add(ValidationResult.Error(
                $"ValueType '{instance.ValueType}' is not supported.",
                [nameof(instance.ValueType)]));
            return errors;
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
            return errors;
        }

        var scopeError = await ConfigEntryValidation.ValidateScopesAsync(instance.Values, _scopeStore, cancellationToken).ConfigureAwait(false);
        if (scopeError is not null)
        {
            errors.Add(ValidationResult.Error(scopeError, [nameof(instance.Values)]));
        }

        return errors;
    }
}