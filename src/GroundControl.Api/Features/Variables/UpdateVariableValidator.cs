using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Validation;
using GroundControl.Persistence.Stores;
using ValidationContext = GroundControl.Api.Shared.Validation.ValidationContext;

namespace GroundControl.Api.Features.Variables;

internal sealed class UpdateVariableValidator : IAsyncValidator<UpdateVariableRequest>
{
    private readonly IScopeStore _scopeStore;

    public UpdateVariableValidator(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateVariableRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        List<ValidationResult> errors = [];

        foreach (var sv in instance.Values)
        {
            foreach (var (dimension, value) in sv.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    errors.Add(ValidationResult.Error($"Scope dimension '{dimension}' does not exist.", [nameof(instance.Values)]));
                }
                else if (!scope.AllowedValues.Contains(value))
                {
                    errors.Add(ValidationResult.Error($"Value '{value}' is not allowed for scope dimension '{dimension}'.", [nameof(instance.Values)]));
                }
            }
        }

        return errors.Count > 0 ? ValidatorResult.ValidationProblem(errors) : ValidatorResult.Success;
    }
}