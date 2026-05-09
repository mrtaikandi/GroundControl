using GroundControl.Api.Features.Clients.Contracts;
using GroundControl.Persistence.Stores;

namespace GroundControl.Api.Features.Clients;

internal sealed class UpdateClientValidator : IAsyncValidator<UpdateClientRequest>
{
    private readonly IScopeStore _scopeStore;

    public UpdateClientValidator(IScopeStore scopeStore)
    {
        _scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
    }

    public async Task<ValidatorResult> ValidateAsync(UpdateClientRequest instance, ValidationContext context, CancellationToken cancellationToken = default)
    {
        var result = new ValidatorResult();

        if (instance.Scopes is { Count: > 0 })
        {
            foreach (var (dimension, value) in instance.Scopes)
            {
                var scope = await _scopeStore.GetByDimensionAsync(dimension, cancellationToken).ConfigureAwait(false);
                if (scope is null)
                {
                    result.AddError($"Scope dimension '{dimension}' was not found.", nameof(instance.Scopes));
                    continue;
                }

                if (!scope.AllowedValues.Contains(value))
                {
                    result.AddError($"Value '{value}' is not allowed for scope dimension '{dimension}'.", nameof(instance.Scopes));
                }
            }
        }

        return result.IsFailed ? result : ValidatorResult.Success;
    }
}
