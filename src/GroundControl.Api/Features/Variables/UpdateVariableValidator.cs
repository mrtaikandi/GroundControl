using GroundControl.Api.Features.Variables.Contracts;
using GroundControl.Api.Shared.Security.Protection;
using GroundControl.Persistence.Stores;

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
        var result = new ValidatorResult();

        if (instance.IsSensitive)
        {
            foreach (var sv in instance.Values)
            {
                if (SensitiveSourceValueProtector.IsMaskSentinel(sv.Value))
                {
                    result.AddError(
                        "Sensitive values cannot be set to the mask sentinel '***'. Submit the actual secret or omit the value.",
                        nameof(instance.Values));
                    return result;
                }
            }
        }

        foreach (var sv in instance.Values)
        {
            foreach (var (dimension, value) in sv.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    result.AddError($"Scope dimension '{dimension}' does not exist.", nameof(instance.Values));
                }
                else if (!scope.AllowedValues.Contains(value))
                {
                    result.AddError($"Value '{value}' is not allowed for scope dimension '{dimension}'.", nameof(instance.Values));
                }
            }
        }

        return result;
    }
}